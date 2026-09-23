using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Interop;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Path = System.IO.Path;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    // Handled is read when the handler first awaits, so the decision must be taken synchronously.
    private async void OnEditorPaste(object sender, TextControlPasteEventArgs e)
    {
        var tab = ViewModel.CurrentNote;
        if (tab is null || NoteSecurity.Blocking) { e.Handled = true; return; }
        DataPackageView view;
        try { view = Clipboard.GetContent(); }
        catch (Exception ex) { DiagnosticsService.LogError("Editor", "Appunti non leggibili", ex); return; }
        if (!view.Contains(StandardDataFormats.StorageItems) && !view.Contains(StandardDataFormats.Bitmap) && !view.Contains(StandardDataFormats.Text))
            return;
        e.Handled = true;
        await PasteIntoEditorAsync(tab, view);
    }

    private async Task PasteIntoEditorAsync(NoteEditorViewModel tab, DataPackageView view)
    {
        bool IsCurrent() => ReferenceEquals(tab, ViewModel.CurrentNote) && !NoteSecurity.Blocking;
        try
        {
            if (view.Contains(StandardDataFormats.StorageItems))
            {
                var items = await view.GetStorageItemsAsync();
                if (await InsertFilesAsync(tab, items))
                {
                    return;
                }
            }

            if (view.Contains(StandardDataFormats.Bitmap) && IsCurrent())
            {
                var path = await SaveClipboardBitmapAsync(view, tab.NoteId);
                if (!IsCurrent()) return;
                if (path is not null)
                {
                    ViewModel.ApplyEdit((t,s,l) => MarkdownEditing.InsertImage(t,s,l,path,"image"));
                    return;
                }
            }

            if (!view.Contains(StandardDataFormats.Text))
            {
                return;
            }

            var pasted = await view.GetTextAsync();
            pasted = DocumentStore.NormalizeNewlines(pasted);
            if (view.Contains(StandardDataFormats.Html) && !HtmlToMarkdown.LooksLikeMarkdown(pasted))
            {
                try
                {
                    var html = await view.GetHtmlFormatAsync();
                    var converted = HtmlToMarkdown.Convert(html);
                    if (!string.IsNullOrWhiteSpace(converted))
                    {
                        pasted = converted;
                    }
                }
                catch (Exception ex)
                {
                    DiagnosticsService.LogError("Editor", "Incolla HTML non convertito", ex);
                }
            }

            if (!IsCurrent()) return;
            if (MarkdownEditing.LooksLikeWebUrl(pasted))
            {
                ViewModel.ApplyEdit((t, s, l) => MarkdownEditing.InsertLink(t, s, l, pasted.Trim()));
            }
            else if (MarkdownTables.TryFromDelimited(pasted, out var table))
            {
                ViewModel.ApplyEdit((t, s, l) => MarkdownEditing.InsertText(t, s, l, table));
            }
            else
            {
                ViewModel.ApplyEdit((t, s, l) => MarkdownEditing.InsertText(t, s, l, pasted));
            }
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Incolla come testo semplice non riuscito", ex);
        }
    }

    /// <summary>
    /// Inserts images into <paramref name="tab"/> first, then opens Markdown files, which changes the
    /// active note. Returns whether any file was used.
    /// </summary>
    private async Task<bool> InsertFilesAsync(NoteEditorViewModel tab, IReadOnlyList<IStorageItem> items)
    {
        var files = items.OfType<StorageFile>().ToArray();
        var handled = false;
        foreach (var file in files.Where(file => LinkResolver.IsSafeLocalImage(file.Path)))
        {
            if (!ReferenceEquals(tab, ViewModel.CurrentNote) || NoteSecurity.Blocking) break;
            await ViewModel.InsertPastedImageAsync(file.Path);
            handled = true;
        }

        foreach (var file in files.Where(file => DocumentStore.IsMarkdown(file.Path)))
        {
            try
            {
                await ViewModel.OpenPathAsync(file.Path);
                handled = true;
            }
            catch (Exception ex)
            {
                DiagnosticsService.LogError("Editor", "Apertura del file trascinato non riuscita", ex);
            }
        }

        return handled;
    }

    private async Task<string?> SaveClipboardBitmapAsync(DataPackageView view, string? noteId)
    {
        var streamRef = await view.GetBitmapAsync();
        using var ras = await streamRef.OpenReadAsync();
        using var source = ras.AsStreamForRead();
        return "attachment://" + await NoteArchive.Database.AddAttachmentAsync(source,"clipboard.png", noteId: noteId);
    }

    private const VirtualKey ImeProcessKey = (VirtualKey)229;

    private void OnEditorPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // While an IME composes, Enter/Tab/brackets confirm or navigate candidates.
        if (composing || e.Key == ImeProcessKey) return;
        if (ShortcutService.TryExecute(e.Key, ViewModel, editorFocus: true)) { e.Handled = true; return; }
        var gesture = ShortcutService.Current(e.Key);
        var control = gesture.Modifiers.HasFlag(MarkdownMkII.Core.Editor.GestureModifiers.Control);
        var shift = gesture.Modifiers.HasFlag(MarkdownMkII.Core.Editor.GestureModifiers.Shift);
        if (gesture.Key == 0 || gesture.Modifiers.HasFlag(MarkdownMkII.Core.Editor.GestureModifiers.Alt)) return;
        if (control && shift && e.Key == VirtualKey.Z)
        {
            if (ViewModel.RedoCommand.CanExecute(null)) ViewModel.RedoCommand.Execute(null);
            e.Handled = true;
            return;
        }
        // Native RichEditBox history must not bypass our Markdown undo history.
        if (control && e.Key is VirtualKey.Z or VirtualKey.Y or VirtualKey.B or VirtualKey.I or VirtualKey.U) { e.Handled = true; return; }
        // Copying protected text is an export: route it through the plaintext confirmation.
        if (control && !shift && e.Key is VirtualKey.C or VirtualKey.X && ViewModel.CurrentNote?.Metadata?.IsProtected == true)
        {
            e.Handled = true;
            CopySelection(cut: e.Key == VirtualKey.X);
            return;
        }
        if (e.Key == VirtualKey.Enter && !control && !shift && ViewModel.TryContinueBlock()) { e.Handled = true; return; }
        if (!control && TryHandleAutoPair(e)) { e.Handled = true; return; }
        if (e.Key == VirtualKey.Tab && !control)
        {
            if (!shift && ViewModel.TryExpandSnippet() || ViewModel.TryAdvanceTableCell(shift)) { e.Handled = true; return; }
            if (shift) ViewModel.Unindent();
            else if (GetSelection().Length > 0) ViewModel.Indent();
            else ViewModel.InsertTabSpaces();
            e.Handled = true;
        }
    }

    private bool TryHandleAutoPair(KeyRoutedEventArgs e)
        => SettingsService.Instance.Editor.AutoPairBrackets &&
           KeyboardText.FromCurrentKey((uint)e.Key, e.KeyStatus.ScanCode) is { } character &&
           MarkdownEditing.IsAutoPairKey(character) &&
           ViewModel.TryAutoPair(character);





































    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        e.Handled = true;
        if (ViewModel.CurrentNote is not { } tab) return;
        try
        {
            await InsertFilesAsync(tab, await e.DataView.GetStorageItemsAsync());
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Trascinamento non riuscito", ex);
        }
    }

    private void OnPreviewWheel(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(PreviewScroller);
        if (!point.Properties.IsHorizontalMouseWheel &&
            (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down)))
        {
            var delta = point.Properties.MouseWheelDelta > 0 ? 0.1 : -0.1;
            var zoom = Math.Clamp(SettingsService.Instance.Preview.Zoom + delta, 0.7, 2.0);
            _ = SettingsService.Instance.UpdateAsync(SettingsArea.Preview, s => s.Preview.Zoom = zoom);
            e.Handled = true;
        }
    }
}

using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Services;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices.WindowsRuntime;
using Path = System.IO.Path;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    public void ApplyText(string text, int selectionStart, int selectionLength)
    {
        if (NoteSecurity.Blocking || App.MainWindow is not MainWindow { IsClosed: false }) return;
        isApplyingText = true;
        try
        {
            EditorBox.Document.GetText(TextGetOptions.None, out var raw);
            var current = ReadEditorText(raw);
            if (current != text)
                ReplaceChangedRange(current, text);
            selectionStart = Math.Clamp(selectionStart, 0, text.Length);
            selectionLength = Math.Clamp(selectionLength, 0, text.Length - selectionStart);
            SetSelection(selectionStart, selectionLength);
            ApplyHighlight();
            if (SettingsService.Instance.Preview.LivePreview) { parseTimer.Stop(); parseTimer.Start(); }
            else RefreshPreview(force: true);
            RebuildLineNumbers();
            RebuildMinimap();
            UpdateStatus();
        }
        finally
        {
            isApplyingText = false;
        }
    }

    // Replacing only the changed span keeps the viewport, IME state and RichEdit layout intact.
    // Story offsets match model offsets because RichEdit stores each line break as a single CR.
    private void ReplaceChangedRange(string current, string text)
    {
        var prefix = 0;
        var limit = Math.Min(current.Length, text.Length);
        while (prefix < limit && current[prefix] == text[prefix]) prefix++;
        var suffix = 0;
        while (suffix < limit - prefix && current[^(suffix + 1)] == text[^(suffix + 1)]) suffix++;
        var range = EditorBox.Document.GetRange(prefix, current.Length - suffix);
        range.SetText(TextSetOptions.None, text.Substring(prefix, text.Length - suffix - prefix));
        EditorBox.Document.GetText(TextGetOptions.None, out var raw);
        if (ReadEditorText(raw) != text)
            EditorBox.Document.SetText(TextSetOptions.None, text);
    }

    private void LoadCurrentNote()
    {
        if (NoteSecurity.Blocking || App.MainWindow is not MainWindow { IsClosed: false }) return;
        isApplyingText = true;
        try { LoadCurrentNoteCore(); }
        finally { isApplyingText = false; }
        UpdateFindCount();
    }

    private void LoadCurrentNoteCore()
    {
        ClearSelectionWork();
        parseTimer.Stop();
        highlightTimer.Stop();
        findRangeStart = 0;
        findRangeLength = -1;
        FindInSelectionBox.IsChecked = false;
        if (boundTab is not null)
        {
            boundTab.PropertyChanged -= OnTabPropertyChanged;
        }

        boundTab = ViewModel.CurrentNote;
        if (boundTab is not null)
        {
            boundTab.PropertyChanged += OnTabPropertyChanged;
        }

        var tab = boundTab;
        FindBox.Text = tab?.FindQuery ?? string.Empty;
        if (tab is null)
        {
            graphRefreshVersion++;
            EditorBox.Document.SetText(TextSetOptions.None, string.Empty);
            PreviewHost.Content = null;
            OutlineList.ItemsSource = null;
            BacklinksList.ItemsSource = null;
            OutgoingList.ItemsSource = null;
            GraphList.ItemsSource = null;
            GraphHost.Content = null;
            TagsLine.Text = string.Empty;
            LineNumbers.Children.Clear();
            UpdateStatus();
            ApplyViewMode(EditorViewMode.Editor);
            CurrentLineHighlight.Visibility = Visibility.Collapsed;
            return;
        }

        EditorBox.Document.SetText(TextSetOptions.None, tab.Text);
        var caret = Math.Clamp(tab.CaretStart, 0, tab.Text.Length);
        EditorBox.Document.Selection.SetRange(caret, caret);

        ApplyHighlight();
        RefreshPreview(force: true);
        RebuildLineNumbers();
        UpdateStatus();
        ApplyViewMode(tab.ViewMode);
        FindBar.Visibility = tab.FindBarOpen ? Visibility.Visible : Visibility.Collapsed;
        UpdateCurrentLineHighlight();
        _ = RefreshBacklinksAsync();
        _ = RefreshOutgoingAsync();
        _ = RefreshGraphAsync();
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (boundTab is null || NoteSecurity.Blocking || App.MainWindow is not MainWindow { IsClosed: false })
        {
            return;
        }

        if (e.PropertyName is nameof(NoteEditorViewModel.ViewMode))
        {
            ApplyViewMode(boundTab.ViewMode);
            RefreshPreview(force: true);
        }
        else if (e.PropertyName is nameof(NoteEditorViewModel.FindBarOpen))
        {
            FindBar.Visibility = boundTab.FindBarOpen ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (e.PropertyName is nameof(NoteEditorViewModel.Header) or nameof(NoteEditorViewModel.Metadata))
        {
            UpdateStatus();
        }
        else if (e.PropertyName is nameof(NoteEditorViewModel.Outline))
        {
            BindOutline();
            ApplyHighlight();
        }
        else if (e.PropertyName is nameof(NoteEditorViewModel.SlideIndex) or nameof(NoteEditorViewModel.KanbanMode) or nameof(NoteEditorViewModel.DiffMode) or nameof(NoteEditorViewModel.DiffAgainstText) or nameof(NoteEditorViewModel.FocusMode))
        {
            BindOutline();
            ApplyHighlight();
            RefreshPreview(force: true);
            if (e.PropertyName is nameof(NoteEditorViewModel.FocusMode))
            {
                ApplyViewMode(boundTab.ViewMode);
            }
        }
    }

    private void OnEditorTextChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || !ReferenceEquals(boundTab, ViewModel.CurrentNote) || NoteSecurity.Blocking || isApplyingText || highlighter.IsApplying || composing || ViewModel.CurrentNote is null)
        {
            return;
        }

        EditorBox.Document.GetText(TextGetOptions.None, out var text);
        text = ReadEditorText(text);
        if (text == ViewModel.CurrentNote.Text) return;
        var selection = GetSelection();
        ViewModel.CurrentNote.SetTextFromEditor(text, selection.Start, selection.Length);
        parseTimer.Stop();
        if (SettingsService.Instance.Preview.LivePreview)
        {
            parseTimer.Start();
        }

        highlightTimer.Stop();
        highlightTimer.Start();
        RebuildLineNumbers();
        RebuildMinimap();
        UpdateStatus();
    }

    private static string ReadEditorText(string text)
    {
        text = text.TrimEnd('\0');
        if (text.EndsWith('\r')) text = text[..^1];
        // RichEdit stores Shift+Enter as a vertical tab and page breaks as form feeds; Markdown has neither.
        return DocumentStore.NormalizeNewlines(text).Replace('\v', '\n').Replace('\f', '\n');
    }

    private void OnEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || !ReferenceEquals(boundTab, ViewModel.CurrentNote) || NoteSecurity.Blocking || isApplyingText || highlighter.IsApplying) return;
        if (ViewModel.CurrentNote is not null && !isApplyingText)
        {
            var selection = GetSelection();
            ViewModel.CurrentNote.UpdateSelection(selection.Start, selection.Length);
        }

        QueueSelectionChrome();
    }

}

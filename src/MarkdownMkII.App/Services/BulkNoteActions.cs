using MarkdownMkII.Services.Localization;
using MarkdownMkII.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownMkII.Services;

internal static class BulkNoteActions
{
    private static bool working;
    public static async Task<IReadOnlyList<(BulkAction Action, int Count)>> CountsAsync(string[] ids, CancellationToken token)
    {
        var notes = await NoteArchive.Database.SummariesAsync(ids, token: token);
        return await Task.Run(() => Enum.GetValues<BulkAction>().Select(a => (a, notes.Count(n => a is BulkAction.Category or BulkAction.Color or BulkAction.AddTags or BulkAction.RemoveTags
            ? !n.Trashed : NoteDatabase.BulkApplies(n, new(a))))).ToArray(), token);
    }
    private static ContentDialog Dialog(string title, object content, string? primary = null)
    {
        var root = (FrameworkElement)App.MainWindow!.Content;
        return new() { Title = title, Content = content, XamlRoot = root.XamlRoot, RequestedTheme = root.ActualTheme,
            PrimaryButtonText = primary ?? "", CloseButtonText = Strings.T("Cancel") };
    }
    public static async Task RunAsync(string[] ids, BulkAction action)
    {
        if (working || NoteSecurity.Blocking || ids.Length == 0) return;
        working = true;
        try { await RunCoreAsync(ids, action); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Operazione multipla fallita", ex);
            if (!NoteSecurity.Blocking) await ArchiveDialogs.MessageAsync(Strings.T("BulkActions"), Strings.T("ArchiveActionFailed"));
        }
        finally { working = false; }
    }
    private static async Task RunCoreAsync(string[] ids, BulkAction action)
    {
        var editor = ((MainWindow)App.MainWindow!).Editor;
        var notes = await NoteArchive.Database.SummariesAsync(ids);
        if (action == BulkAction.Protect || notes.Any(n => n.IsProtected))
            if (!await NoteSecurity.EnsureUnlockedAsync()) return;
        var session = NoteArchive.Database.SessionVersion;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(NoteSecurity.OperationsToken);
        var token = cancellation.Token;
        var request = new BulkRequest(action, CopySuffix: Strings.T("ArchiveCopySuffix"));
        if (action == BulkAction.Category)
        {
            var choices = new[] { new NamedLabel("", Strings.T("ArchiveUncategorized"), 0) }.Concat(await NoteArchive.Database.LabelsAsync(true)).ToArray();
            var list = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Name", SelectedIndex = 0, MinWidth = 280 };
            if (await Dialog(Strings.T("BulkCategory"), list, Strings.T("Ok")).ShowAsync() != ContentDialogResult.Primary) return;
            request = request with { CategoryId = (list.SelectedItem as NamedLabel)?.Id is { Length: > 0 } id ? id : null };
        }
        else if (action is BulkAction.AddTags or BulkAction.RemoveTags)
        {
            var text = await ArchiveDialogs.PromptAsync(Strings.T("Bulk" + action) + " — " + Strings.T("BulkTagsHint"));
            if (text is null) return;
            var tags = text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(t => NoteDatabase.CleanLabel(t.Trim().TrimStart('#'))).Where(t => t.Length > 0).ToArray();
            if (tags.Length == 0) return;
            request = request with { Tags = tags };
        }
        else if (action == BulkAction.Color)
        {
            var colors = Enum.GetValues<NoteColor>();
            var list = new ComboBox { ItemsSource = colors.Select(c => Strings.T("NoteColor" + c)).ToArray(), SelectedIndex = 0, MinWidth = 240 };
            if (await Dialog(Strings.T("BulkColor"), list, Strings.T("Ok")).ShowAsync() != ContentDialogResult.Primary) return;
            request = request with { Color = colors[list.SelectedIndex] };
        }
        if (token.IsCancellationRequested || session != NoteArchive.Database.SessionVersion) return;
        notes = await NoteArchive.Database.SummariesAsync(ids, token: token);
        var count = await Task.Run(() => notes.Count(n => NoteDatabase.BulkApplies(n, request)), token);
        if (count == 0) return;
        if (action is BulkAction.Trash or BulkAction.Delete)
            if (await Dialog(Strings.T("Bulk" + action), Strings.Format(Strings.DefaultMap, "BulkConfirm", count), Strings.T("Bulk" + action)).ShowAsync() != ContentDialogResult.Primary) return;
        if (action == BulkAction.Unprotect && !await NoteSecurity.ConfirmPlaintextAsync(removing: true)) return;
        if (action == BulkAction.Export && !await NoteSecurity.AuthorizeExportAsync(ids)) return;
        if (token.IsCancellationRequested || session != NoteArchive.Database.SessionVersion) return;
        if (editor.CurrentNote?.NoteId is { } current && ids.Contains(current))
        {
            if (!await editor.FlushAsync(true)) return;
            if (action is BulkAction.Trash or BulkAction.Delete or BulkAction.Archive or BulkAction.Protect or BulkAction.Unprotect)
            {
                await editor.CloseEditorAsync();
                if (editor.CurrentNote is not null) return;
            }
        }
        string? destination = null;
        if (action == BulkAction.Export)
        {
            destination = (await FilePickerService.PickFolderAsync())?.Path;
            if (destination is null) return;
        }
        if (token.IsCancellationRequested) return;
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 12, MinWidth = 300 };
        panel.Children.Add(status); panel.Children.Add(new ProgressBar { IsIndeterminate = true });
        var progressDialog = Dialog(Strings.T("Bulk" + action), panel);
        progressDialog.CloseButtonClick += (_, e) => { e.Cancel = true; cancellation.Cancel(); progressDialog.IsEnabled = false; };
        var showing = progressDialog.ShowAsync();
        BulkResult? result = null;
        try
        {
            if (action == BulkAction.Export)
            {
                var progress = new Progress<BulkProgress>(p => status.Text = Strings.Format(Strings.DefaultMap, "BulkProgress", p.Completed + p.Failed, p.Total));
                result = await NoteArchive.Transfer.ExportDetailedAsync(notes.Where(n => !n.Trashed).Select(n => n.Id), destination!, progress, token);
                result = result with { Unchanged = result.Unchanged + ids.Length - count };
            }
            else
            {
                if (action is BulkAction.Protect or BulkAction.Unprotect) await NoteSecurity.ClearAttachmentCacheAsync();
                var progress = new Progress<BulkProgress>(p => status.Text = Strings.Format(Strings.DefaultMap, "BulkProgress", p.Completed + p.Unchanged + p.Failed, p.Total));
                result = await NoteArchive.Database.ApplyBulkAsync(ids, request, progress, token);
                NoteArchive.NotifyChanged(action == BulkAction.Delete ? ArchiveChangeKind.Removed : ArchiveChangeKind.Metadata, result.ChangedIds);
                if (result.CreatedIds.Count > 0) NoteArchive.NotifyChanged(ArchiveChangeKind.Inserted, result.CreatedIds);
            }
        }
        finally { progressDialog.Hide(); await showing; }
        if (result is null || token.IsCancellationRequested && session != NoteArchive.Database.SessionVersion || NoteSecurity.Blocking) return;
        var message = Strings.Format(Strings.DefaultMap, "BulkResult", result.ChangedIds.Count + result.CreatedIds.Count,
            result.Unchanged, result.FailedIds.Count, result.NotExecuted);
        if (result.MaintenancePending) message += "\n" + Strings.T("SecurityMaintenancePending");
        await ArchiveDialogs.MessageAsync(Strings.T("BulkActions"), message);
    }
}

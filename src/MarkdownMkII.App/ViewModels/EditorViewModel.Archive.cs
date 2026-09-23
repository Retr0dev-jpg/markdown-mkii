using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.Storage;
using Windows.ApplicationModel.DataTransfer;

namespace MarkdownMkII.ViewModels;
public partial class EditorViewModel
{
    private readonly SemaphoreSlim navigationGate = new(1, 1);
    private readonly MarkdownMkII.Core.Editor.LatestSaveQueue<PendingSave> saves = new();
    private sealed record PendingSave(NoteEditorViewModel Tab, EditorSaveSnapshot Snapshot, long Version);
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer saveDelay;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer saveDeadline;
    [ObservableProperty]
    public partial string? SaveError { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }
    public string SaveStatus => SaveError is not null ? SaveError : IsSaving || HasUnsavedChanges ? Strings.T("ArchiveSaving") : Strings.T("WorkspaceSaved");

    public EditorViewModel()
    {
        NoteSecurity.Changed += (_, _) => NotifySecurityCommands();
        saves.StateChanged += (_, _) => IsSaving = saves.IsSaving;
        NoteArchive.Changed += async (_, change) =>
        {
            var tab = CurrentNote;
            if (tab?.NoteId is null || (change.Kind != ArchiveChangeKind.Reset && !change.Ids.Contains(tab.NoteId)))
                return;
            try
            {
                var summary = change.Summaries.FirstOrDefault(n => n.Id == tab.NoteId) ?? await NoteArchive.Database.SummaryAsync(tab.NoteId);
                if (summary is not null && ReferenceEquals(tab, CurrentNote))
                    tab.UpdateArchiveMetadata(summary);
            }
            catch (Exception ex)
            {
                DiagnosticsService.LogError("Archive", "Aggiornamento metadati fallito", ex);
            }
        };
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        saveDelay = dispatcher.CreateTimer();
        saveDelay.IsRepeating = false;
        saveDelay.Interval = TimeSpan.FromMilliseconds(750);
        saveDeadline = dispatcher.CreateTimer();
        saveDeadline.IsRepeating = false;
        saveDeadline.Interval = TimeSpan.FromSeconds(5);
        saveDelay.Tick += async (_, _) => await FlushAsync();
        saveDeadline.Tick += async (_, _) => await FlushAsync();
    }

    partial void OnCurrentNoteChanged(NoteEditorViewModel? oldValue, NoteEditorViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= OnTabStateChanged;
        if (newValue is not null)
            newValue.PropertyChanged += OnTabStateChanged;
        NotifySelectionCommands();
        NotifySecurityCommands();
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(SaveStatus));
    }

    private void OnTabStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NoteEditorViewModel.Text))
        {
            SaveError = null;
            saveDelay.Stop();
            saveDelay.Start();
            if (!saveDeadline.IsRunning)
                saveDeadline.Start();
        }

        if (e.PropertyName is nameof(NoteEditorViewModel.Text) or nameof(NoteEditorViewModel.IsDirty))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(SaveStatus));
        }

        if (e.PropertyName == nameof(NoteEditorViewModel.CanUndo))
            UndoCommand.NotifyCanExecuteChanged();
        if (e.PropertyName == nameof(NoteEditorViewModel.CanRedo))
            RedoCommand.NotifyCanExecuteChanged();
    }

    partial void OnSaveErrorChanged(string? value) => OnPropertyChanged(nameof(SaveStatus));
    partial void OnIsSavingChanged(bool value) => OnPropertyChanged(nameof(SaveStatus));
    public async Task<bool> FlushAsync(bool checkpoint = false)
    {
        if (HasVolatileProtectedDraft && CurrentNote is null) { SaveError = Strings.T("SecurityPendingSave"); return false; }
        checkpoint |= SaveError is not null;
        saveDelay.Stop();
        saveDeadline.Stop();
        SaveError = null;
        try
        {
            var wrote = await saves.FlushAsync(force =>
            {
                if (CurrentNote is not { NoteId: not null } tab || (!force && !tab.IsDirty))
                    return null;
                return new PendingSave(tab, tab.CaptureSaveSnapshot(), tab.Version);
            }, async (pending, force) =>
            {
                var saved = await NoteArchive.Database.SaveAsync(pending.Tab.NoteId!, pending.Snapshot.Text, pending.Version, force);
                if (saved.Markdown != pending.Snapshot.Text && pending.Tab.Text == pending.Snapshot.Text)
                {
                    ApplyEdit(pending.Tab, (text, start, length) => MarkdownEditing.MapSelection(text, saved.Markdown, start, length), focus: false);
                    pending.Tab.CompleteArchiveSave(new EditorSaveSnapshot(saved.Markdown), saved.Version);
                }
                else pending.Tab.CompleteArchiveSave(pending.Snapshot, saved.Version);
                OnNoteSaved(saved.Summary.Id);
                if (saved.Version != pending.Version)
                {
                    pending.Tab.UpdateArchiveMetadata(saved.Summary);
                    NoteArchive.NotifyChanged(ArchiveChangeKind.Content, saved.Summary);
                }
            }, checkpoint);

            if (wrote) _ = RetryProtectionMaintenanceAsync();
            return true;
        }
        catch (Exception ex)
        {
            SaveError = Strings.T("ArchiveSaveFailed");
            DiagnosticsService.LogError("Archive", "Salvataggio nota non riuscito", ex);
            if (ex is not (NoteConflictException or NoteLockedException) && CurrentNote?.IsDirty == true) saveDeadline.Start();
            return false;
        }
    }

    private DateTimeOffset lastMaintenanceAttempt = DateTimeOffset.MinValue;

    // Cleanup after a failed protection change runs outside the save, so it can never make a save fail.
    private async Task RetryProtectionMaintenanceAsync()
    {
        if (NoteSecurity.Blocking || DateTimeOffset.UtcNow - lastMaintenanceAttempt < TimeSpan.FromMinutes(5)) return;
        lastMaintenanceAttempt = DateTimeOffset.UtcNow;
        try
        {
            if ((await NoteArchive.Database.ProtectionSettingsAsync()).MaintenancePending)
                await NoteSecurity.RetryMaintenanceAsync();
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Security", "Manutenzione dell'archivio rinviata", ex);
        }
    }

    [RelayCommand]
    private void RefreshPreview() => host?.RefreshPreviewNow();
    [RelayCommand]
    private void ShowReplace() => host?.ShowReplace();
    [RelayCommand]
    private void ToggleInspector() => host?.ToggleInspector();
    [RelayCommand]
    private void ToggleTask() => TryToggleTask();
    [RelayCommand]
    private Task ZoomInAsync() => ZoomAsync(1);
    [RelayCommand]
    private Task ZoomOutAsync() => ZoomAsync(-1);
    private static Task ZoomAsync(int delta) => SettingsService.Instance.UpdateAsync(MarkdownMkII.Core.Models.SettingsArea.Editor, s => s.Editor.FontSize = Math.Clamp(s.Editor.FontSize + delta, 10, 32));
    [RelayCommand]
    public Task SaveAsync() => FlushAsync();
    public async Task OpenNoteAsync(string id)
    {
        if (NoteSecurity.Blocking || !await NoteSecurity.EnsureNoteAccessAsync(id)) return;
        if (HasVolatileProtectedDraft && !await NoteSecurity.EnsureUnlockedAsync()) return;
        var session = NoteArchive.Database.SessionVersion;
        await navigationGate.WaitAsync();
        try
        {
            if (NoteSecurity.Blocking) return;
            if (CurrentNote?.NoteId == id)
            {
                NavigateToEditor();
                return;
            }

            var note = await NoteArchive.Database.GetAsync(id);
            if (note is null || note.Summary.Trashed)
                return;
            // Load first; edits made while reading must be included in the final flush.
            if (!await FlushAsync(true))
                return;
            if (NoteSecurity.Blocking || session != NoteArchive.Database.SessionVersion) return;
            var previous = CurrentNote;
            var tab = new NoteEditorViewModel();
            tab.LoadArchiveNote(note);
            CurrentNote = tab;
            if (previous?.Metadata?.IsProtected == true) previous.ClearSensitiveState();
            NavigateToEditor();
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Apertura nota non riuscita", ex);
            SaveError = Strings.T("ArchiveOpenFailed");
        }
        finally
        {
            navigationGate.Release();
        }
    }

    [RelayCommand]
    public async Task NewDocumentAsync()
    {
        if (NoteSecurity.Blocking) return;
        if (!await FlushAsync(true))
            return;
        var note = await NoteArchive.Database.CreateAsync(Strings.T("Untitled"));
        NoteArchive.NotifyChanged(ArchiveChangeKind.Inserted, note.Summary);
        await OpenNoteAsync(note.Summary.Id);
    }

    [RelayCommand]
    public async Task CloseEditorAsync(NoteEditorViewModel? tab = null)
    {
        await navigationGate.WaitAsync();
        try
        {
            if (!await FlushAsync(true))
            {
                NavigateToEditor();
                return;
            }

            var previous = CurrentNote;
            CurrentNote = null;
            if (previous?.Metadata?.IsProtected == true) previous.ClearSensitiveState();
            if (App.MainWindow is MainWindow window)
                window.ShowEmptyWorkspace();
        }
        finally
        {
            navigationGate.Release();
        }
    }

    [RelayCommand]
    public Task OpenAsync() => ArchiveDialogs.ImportAsync();
    public Task OpenPathAsync(string path) => ArchiveDialogs.ImportAsync([path]);
    public Task OpenOrCreateMarkdownAsync(string target) => OpenTargetAsync(target);
    [RelayCommand]
    public Task ExportMarkdownAsync() => CurrentNote?.NoteId is { } id ? ArchiveDialogs.ExportAsync([id]) : Task.CompletedTask;
    [RelayCommand]
    public async Task ExportHtmlAsync()
    {
        if (CurrentNote is not { NoteId: not null } tab)
            return;
        var file = await FilePickerService.SaveHtmlAsync(tab.Title + ".html");
        if (file is null)
            return;
        await ArchiveDialogs.ExportHtmlAsync(tab.NoteId, file.Path);
    }

    [RelayCommand]
    public async Task RenameAsync()
    {
        if (CurrentNote?.NoteId is not { } id)
            return;
        await ArchiveDialogs.RenameAsync(id);
    }

    [RelayCommand]
    public async Task DuplicateDocumentAsync()
    {
        if (CurrentNote?.NoteId is not { } id || !await FlushAsync(true))
            return;
        await ArchiveDialogs.DuplicateAsync(id);
    }

    [RelayCommand]
    public async Task ToggleStarAsync()
    {
        if (CurrentNote?.NoteId is not { } id)
            return;
        await ArchiveDialogs.ToggleFavoriteAsync(id);
    }

    public void RefreshNoteMetadata(NoteSummary note)
    {
        if (CurrentNote?.NoteId == note.Id)
            CurrentNote.UpdateArchiveMetadata(note);
    }

    [RelayCommand]
    public async Task RestoreHistoryAsync()
    {
        if (CurrentNote is not { NoteId: not null } tab || host is null || !await FlushAsync())
            return;
        var items = await NoteArchive.Database.RevisionsAsync(tab.NoteId);
        var labels = items.Select((r, i) => $"{r.Created.LocalDateTime:g} · {i + 1}").ToArray();
        var chosen = await host.PickAsync(Strings.T("CmdHistory.Label"), Strings.T("HistoryEmpty"), labels);
        var index = Array.IndexOf(labels, chosen);
        if (index < 0)
            return;
        ApplyEdit(tab, (_, _, _) => new EditResult(items[index].Markdown, 0, 0));
    }

    [RelayCommand]
    public async Task ImageAsync()
    {
        var file = await FilePickerService.OpenImageAsync();
        if (file is not null)
            await InsertPastedImageAsync(file.Path);
    }

    public async Task InsertPastedImageAsync(string path)
    {
        if (CurrentNote is not { } tab)
            return;
        string id;
        try
        {
            await using var stream = File.OpenRead(path);
            id = await NoteArchive.Database.AddAttachmentAsync(stream, Path.GetFileName(path), noteId: tab.NoteId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DiagnosticsService.LogError("Editor", "Immagine non allegata", ex);
            await ArchiveDialogs.MessageAsync(Strings.T("ArchiveActionFailed"), ex.Message);
            return;
        }
        if (!ReferenceEquals(CurrentNote, tab))
            return;
        ApplyEdit(tab, (t, s, l) => MarkdownEditing.InsertImage(t, s, l, "attachment://" + id, Path.GetFileNameWithoutExtension(path)));
    }

    [RelayCommand]
    public async Task WikiAsync()
    {
        if (CurrentNote is not { } tab)
            return;
        var note = await ArchiveDialogs.PickNoteAsync();
        if (note is null || !ReferenceEquals(tab, CurrentNote))
            return;
        // With hidden details a protected title must not be copied into another note's text.
        var label = note.IsProtected && (await NoteArchive.Database.ProtectionSettingsAsync()).HideDetails
            ? Strings.T("SecurityProtectedNote")
            : note.Title;
        if (!ReferenceEquals(tab, CurrentNote)) return;
        ApplyEdit(tab, (t, s, l) => MarkdownEditing.InsertLink(t, s, l, "note://" + note.Id, label));
    }

    public async Task OpenTargetAsync(string target)
    {
        var(path, heading) = MarkdownMkII.Core.Preview.LinkResolver.SplitFragment(target);
        if (path.Length == 0)
        {
            if (heading is not null)
                GoToHeading(heading);
            return;
        }

        if (path.StartsWith("wiki:", StringComparison.OrdinalIgnoreCase))
            path = Uri.UnescapeDataString(path[5..].TrimStart('/'));
        if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            path = path[..^3];
        var matches = await NoteArchive.Database.ResolveAsync(path);
        if (matches.Count == 0 && !NoteArchive.Database.IsUnlocked && (await NoteArchive.Database.ProtectionSettingsAsync()).Configured)
        {
            if (!await NoteSecurity.EnsureUnlockedAsync()) return;
            matches = await NoteArchive.Database.ResolveAsync(path);
        }
        var selected = matches.Count == 1 ? matches[0] : matches.Count > 1 ? await ArchiveDialogs.PickNoteAsync(matches) : null;
        if (selected is null)
            return;
        await OpenNoteAsync(selected.Id);
        if (CurrentNote?.NoteId == selected.Id && heading is not null)
            GoToHeading(heading);
    }

    [RelayCommand]
    public async Task GoToDefinitionAsync()
    {
        if (CurrentNote is not { } tab || host is null)
            return;
        var(start, _) = host.GetSelection();
        var wiki = WikiLinks.AtCaret(tab.Text, start);
        if (wiki is { } link)
        {
            await OpenTargetAsync(link.Target + (link.Heading is null ? "" : "#" + link.Heading));
            return;
        }

        var markdown = WikiLinks.MarkdownLinkAt(tab.Text, start);
        if (markdown is { } md)
            await OpenTargetAsync(md.Url);
    }

    [RelayCommand]
    public async Task RenameHeadingAsync()
    {
        if (CurrentNote is not { } tab || host is null)
            return;
        var title = await host.PromptAsync(Strings.T("CmdRenameHeading.Label"), "");
        if (string.IsNullOrWhiteSpace(title))
            return;
        ApplyEdit(tab, (t, s, l) => MarkdownEditing.RenameHeading(t, s, title)?.Result ?? new(t, s, l));
    }

    [RelayCommand]
    public async Task ExtractToNoteAsync()
    {
        if (CurrentNote is not { } tab || host is null)
            return;
        var(start, length) = host.GetSelection();
        if (length == 0)
            return;
        var title = await host.PromptAsync(Strings.T("CmdExtractNote.Label"), Strings.T("Untitled"));
        if (string.IsNullOrWhiteSpace(title) || !ReferenceEquals(tab, CurrentNote))
            return;
        var note = tab.Metadata?.IsProtected == true
            ? await NoteArchive.Database.CreateProtectedAsync(title, tab.Text.Substring(start, length), tab.NoteId)
            : await NoteArchive.Database.CreateAsync(title, tab.Text.Substring(start, length));
        ApplyEdit(tab, (t, s, l) => MarkdownEditing.InsertText(t, start, length, $"[{title}](note://{note.Summary.Id})"));
        NoteArchive.NotifyChanged(ArchiveChangeKind.Inserted, note.Summary);
    }

    [RelayCommand]
    public void CopyHtml()
    {
        if (CurrentNote is not { } tab)
            return;
        var html = MarkdownMkII.Core.DocumentParser.ToHtml(tab.Text);
        _ = CopyToClipboardAsync(html, html);
    }

    /// <summary>Copies note content, asking first when the note is protected.</summary>
    private async Task CopyToClipboardAsync(string text, string? html = null)
    {
        try
        {
            if (!await NoteSecurity.ConfirmCopyAsync(CurrentNote)) return;
            var package = new DataPackage();
            package.SetText(text);
            if (html is not null) package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(html));
            Clipboard.SetContent(package);
        }
        catch (Exception ex) { DiagnosticsService.LogError("Editor", "Copia negli appunti non riuscita", ex); }
    }

    private bool HasSavedNote() => CurrentNote?.NoteId is not null;
    [RelayCommand]
    public Task NewDailyAsync() => PeriodicAsync(DateTime.Today.ToString("yyyy-MM-dd"), "daily");
    [RelayCommand]
    public Task NewYesterdayAsync() => PeriodicAsync(DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"), "daily");
    [RelayCommand]
    public Task NewTomorrowAsync() => PeriodicAsync(DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"), "daily");
    [RelayCommand]
    public Task NewWeeklyAsync() => PeriodicAsync($"{DateTime.Today.Year}-W{System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Today):00}", "weekly");
    [RelayCommand]
    public Task NewMonthlyAsync() => PeriodicAsync(DateTime.Today.ToString("yyyy-MM"), "monthly");
    [RelayCommand]
    public Task NewQuarterlyAsync() => PeriodicAsync($"{DateTime.Today.Year}-Q{(DateTime.Today.Month - 1) / 3 + 1}", "quarterly");
    [RelayCommand]
    public Task NewYearlyAsync() => PeriodicAsync(DateTime.Today.ToString("yyyy"), "yearly");
    private async Task PeriodicAsync(string title, string template)
    {
        var matches = await NoteArchive.Database.ResolveAsync(title);
        var id = matches.FirstOrDefault()?.Id;
        if (id is null)
        {
            id = (await NoteArchive.Database.CreateAsync(title, MarkdownTemplates.Render(template, title))).Summary.Id;
            NoteArchive.NotifyChanged(ArchiveChangeKind.Inserted, [id]);
        }

        await OpenNoteAsync(id);
    }

    [RelayCommand]
    public async Task NewFromTemplateAsync()
    {
        var note = await NoteArchive.Database.CreateAsync(Strings.T("Untitled"), MarkdownTemplates.Render(SettingsService.Instance.Library.DefaultTemplate, Strings.T("Untitled")));
        NoteArchive.NotifyChanged(ArchiveChangeKind.Inserted, note.Summary);
        await OpenNoteAsync(note.Summary.Id);
    }
}

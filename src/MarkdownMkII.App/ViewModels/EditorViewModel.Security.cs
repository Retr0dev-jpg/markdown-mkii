using CommunityToolkit.Mvvm.Input;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.Storage;
using System.Security.Cryptography;

namespace MarkdownMkII.ViewModels;

public partial class EditorViewModel
{
    private SealedNoteDraft? protectedDraft = LoadStoredDraft();
    public bool HasProtectedDraft => protectedDraft is not null;
    /// <summary>A pending draft that only lives in memory; one stored on disk survives closing the app.</summary>
    public bool HasVolatileProtectedDraft => protectedDraft is not null && !File.Exists(DraftPath);

    // The sealed draft is AES-GCM ciphertext under the note key; persisting it lets an unsaved protected
    // edit survive a crash or a forced close. No key material is written.
    private static string DraftPath => Path.Combine(AppPaths.Root, "sealed-draft.json");
    private sealed record StoredDraft(string NoteId, long Version, string Data);

    private static SealedNoteDraft? LoadStoredDraft()
    {
        try
        {
            if (!File.Exists(DraftPath)) return null;
            var stored = System.Text.Json.JsonSerializer.Deserialize<StoredDraft>(File.ReadAllText(DraftPath));
            return stored is null ? null : new SealedNoteDraft(stored.NoteId, stored.Version, Convert.FromBase64String(stored.Data));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException)
        {
            DiagnosticsService.LogError("Security", "Bozza protetta salvata non leggibile", ex);
            return null;
        }
    }

    private static void StoreDraft(SealedNoteDraft draft)
    {
        var temporary = DraftPath + ".tmp";
        File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(new StoredDraft(draft.NoteId, draft.Version, Convert.ToBase64String(draft.Data))));
        File.Move(temporary, DraftPath, overwrite: true);
    }

    private void ForgetDraft()
    {
        if (protectedDraft is { } draft) CryptographicOperations.ZeroMemory(draft.Data);
        protectedDraft = null;
        try { if (File.Exists(DraftPath)) File.Delete(DraftPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { DiagnosticsService.LogError("Security", "Bozza protetta non eliminata", ex); }
    }

    [RelayCommand(CanExecute = nameof(CanProtectNote))]
    private Task ProtectNoteAsync() => CurrentNote?.NoteId is { } id ? NoteSecurity.ProtectNoteAsync(id) : Task.CompletedTask;
    [RelayCommand(CanExecute = nameof(CanRemoveNoteProtection))]
    private Task RemoveNoteProtectionAsync() => CurrentNote?.NoteId is { } id ? NoteSecurity.ProtectNoteAsync(id, true) : Task.CompletedTask;
    [RelayCommand(CanExecute = nameof(CanLockNotes))]
    private Task LockNotesAsync() => NoteSecurity.LockAsync();
    [RelayCommand(CanExecute = nameof(CanUnlockNotes))]
    private async Task UnlockNotesAsync() => await NoteSecurity.EnsureUnlockedAsync();

    private bool CanProtectNote() => !NoteSecurity.Blocking && CurrentNote?.Metadata?.IsProtected == false;
    private bool CanRemoveNoteProtection() => !NoteSecurity.Blocking && CurrentNote?.Metadata?.IsProtected == true;
    private static bool CanLockNotes() => NoteArchive.Database.IsUnlocked;
    private bool CanUnlockNotes() => !NoteSecurity.Blocking && NoteArchive.Database.IsProtectionConfigured && (!NoteArchive.Database.IsUnlocked || HasProtectedDraft);
    private void NotifySecurityCommands()
    {
        ProtectNoteCommand.NotifyCanExecuteChanged();
        RemoveNoteProtectionCommand.NotifyCanExecuteChanged();
        LockNotesCommand.NotifyCanExecuteChanged();
        UnlockNotesCommand.NotifyCanExecuteChanged();
    }

    internal async Task SealAndCloseProtectedAsync()
    {
        await navigationGate.WaitAsync();
        try
        {
            if (CurrentNote is { Metadata.IsProtected: true, NoteId: not null } tab)
            {
                if (!await FlushAsync(true))
                {
                    protectedDraft = await NoteArchive.Database.SealDraftAsync(tab.NoteId, tab.Text, tab.Version);
                    try { StoreDraft(protectedDraft); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // The draft still lives in memory; only crash survival is lost.
                        DiagnosticsService.LogError("Security", "Bozza protetta non salvata su disco", ex);
                    }
                }
                CurrentNote = null;
                tab.ClearSensitiveState();
                if (App.MainWindow is MainWindow window) window.ShowEmptyWorkspace();
            }
            else if (CurrentNote is { } ordinary)
            {
                ordinary.NoteResources = null;
                ordinary.Parsed = null;
                ordinary.DiffAgainstText = null;
            }
        }
        finally { navigationGate.Release(); }
    }

    /// <summary>Closes the editor without saving and forgets any sealed draft; used before erasing the archive.</summary>
    internal async Task DiscardForResetAsync()
    {
        await navigationGate.WaitAsync();
        try
        {
            saveDelay.Stop();
            saveDeadline.Stop();
            var previous = CurrentNote;
            CurrentNote = null;
            previous?.ClearSensitiveState();
            ForgetDraft();
            SaveError = null;
            if (App.MainWindow is MainWindow window) window.ShowEmptyWorkspace();
        }
        finally { navigationGate.Release(); }
        NotifySecurityCommands();
    }

    internal async Task RestoreProtectedDraftAsync()
    {
        if (protectedDraft is not { } draft || !NoteArchive.Database.IsUnlocked) return;
        var lost = false;
        await navigationGate.WaitAsync();
        try
        {
            if (CurrentNote is not null && !await FlushAsync(true)) return;
            var session = NoteArchive.Database.SessionVersion;
            string text;
            NoteDocument? document;
            try
            {
                document = await NoteArchive.Database.GetAsync(draft.NoteId);
                text = document is null ? "" : await NoteArchive.Database.UnsealDraftAsync(draft);
            }
            catch (Exception ex) when (ex is CryptographicException or KeyNotFoundException or InvalidDataException)
            {
                document = null;
                text = "";
            }

            if (document is null)
            {
                // The note (and its key) no longer exists: the draft can never be decrypted again.
                ForgetDraft();
                lost = true;
                return;
            }

            if (session != NoteArchive.Database.SessionVersion || NoteSecurity.Blocking) return;
            if (document.Version != draft.Version)
            {
                // Preserve both versions if another writer changed the note while it was locked.
                document = await NoteArchive.Database.CreateProtectedAsync(
                    Strings.Format(Strings.DefaultMap, "SecurityRecoveredTitle", document.Summary.Title), text, draft.NoteId);
                text = document.Markdown;
            }
            if (session != NoteArchive.Database.SessionVersion || NoteSecurity.Blocking) return;
            var tab = new NoteEditorViewModel();
            tab.LoadArchiveNote(document);
            tab.Text = text;
            CurrentNote = tab;
            protectedDraft = null;
            CryptographicOperations.ZeroMemory(draft.Data);
            NavigateToEditor();
            // Keep the file until the restored text is really stored; the next successful save removes it.
            storedDraftNoteId = document.Summary.Id;
            if (await FlushAsync(true)) ForgetDraft();
        }
        catch { SaveError = Strings.T("SecurityPendingSave"); }
        finally { navigationGate.Release(); }
        if (lost) await ArchiveDialogs.MessageAsync(Strings.T("SecurityTitle"), Strings.T("SecurityDraftLost"));
    }

    private string? storedDraftNoteId;

    private void OnNoteSaved(string noteId)
    {
        if (storedDraftNoteId != noteId || protectedDraft is not null) return;
        storedDraftNoteId = null;
        ForgetDraft();
    }
}

using System.Text;
using System.Diagnostics;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.Storage;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace MarkdownMkII.Services;

public static class NoteSecurity
{
    private static MainWindow? window;
    private static FrameworkElement? root;
    private static Microsoft.UI.Dispatching.DispatcherQueueTimer? idleTimer;
    private static Interop.SecuritySessionMonitor? monitor;
    private static long lastInput = Stopwatch.GetTimestamp();
    private static long epoch;
    private static bool monitorAvailable;
    private static Task<bool>? unlocking;
    private static Task? locking;
    private static CancellationTokenSource operations = new();
    public static CancellationToken OperationsToken => operations.Token;
    private static int idleMinutes = 5;
    public static bool Blocking { get; private set; }
    public static event EventHandler? Clearing;
    public static event EventHandler? Changed;
    private static nint Handle => WinRT.Interop.WindowNative.GetWindowHandle(window!);

    internal static void Initialize(MainWindow owner, FrameworkElement surface)
    {
        window = owner;
        root = surface;
        root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => Touch()), true);
        root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => Touch()), true);
        root.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler((_, _) => Touch()), true);
        root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => Touch()), true);
        idleTimer = owner.DispatcherQueue.CreateTimer();
        idleTimer.Interval = TimeSpan.FromSeconds(1);
        idleTimer.Tick += (_, _) =>
        {
            // Dialog input bypasses the main tree; file pickers are separate windows and pause the clock.
            if (idlePauses > 0 || Interop.UserActivity.HadRecentInput(Handle, TimeSpan.FromSeconds(2))) Touch();
            if (NoteArchive.Database.IsUnlocked && Stopwatch.GetElapsedTime(lastInput) >= TimeSpan.FromMinutes(idleMinutes)) _ = LockAsync();
        };
        idleTimer.Start();
        try { monitor = new(Handle, () => _ = LockAsync()); monitorAvailable = true; }
        catch { monitorAvailable = false; }
        NoteArchive.Database.ProtectionChanged += OnProtectionChanged;
    }

    private static void OnProtectionChanged(object? sender, EventArgs e)
    {
        if (window is not { IsClosed: false } owner) return;
        owner.DispatcherQueue.TryEnqueue(() =>
        {
            if (owner.IsClosed) return;
            if (NoteArchive.Database.IsUnlocked && operations.IsCancellationRequested) operations = new();
            NoteArchive.NotifyChanged();
            Changed?.Invoke(null, EventArgs.Empty);
        });
    }

    internal static void Dispose()
    {
        NoteArchive.Database.ProtectionChanged -= OnProtectionChanged;
        epoch++;
        operations.Cancel();
        idleTimer?.Stop();
        monitor?.Dispose();
    }
    public static void Touch() => lastInput = Stopwatch.GetTimestamp();

    private static int idlePauses;

    /// <summary>Suspends the idle lock while a system file picker (a separate window) is open.</summary>
    public static IDisposable PauseIdle()
    {
        Interlocked.Increment(ref idlePauses);
        return new IdlePause();
    }

    private sealed class IdlePause : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 1) return;
            Interlocked.Decrement(ref idlePauses);
            Touch();
        }
    }
    public static async Task RefreshSettingsAsync(bool notify = false)
    {
        idleMinutes = (await NoteArchive.Database.ProtectionSettingsAsync()).IdleMinutes;
        if (notify) Changed?.Invoke(null, EventArgs.Empty);
    }
    public static Task<bool> EnsureUnlockedAsync()
    {
        if (unlocking is not null) return unlocking;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        unlocking = completion.Task;
        _ = CompleteUnlockAsync(completion);
        return completion.Task;
    }
    private static async Task CompleteUnlockAsync(TaskCompletionSource<bool> completion)
    {
        try
        {
            if (NoteArchive.Database.IsUnlocked && !Blocking)
            {
                await window!.Editor.RestoreProtectedDraftAsync();
                completion.SetResult(NoteArchive.Database.IsUnlocked && !window.Editor.HasProtectedDraft && !Blocking);
            }
            else completion.SetResult(await UnlockCoreAsync());
        }
        catch { completion.SetResult(false); }
        finally { unlocking = null; }
    }
    private static int failedUnlocks;

    private static async Task<bool> UnlockCoreAsync()
    {
        var generation = epoch;
        try
        {
            if (locking is not null) await locking;
            if (Blocking) return false;
            if (!monitorAvailable) { await ArchiveDialogs.MessageAsync(Strings.T("SecurityTitle"), Strings.T("SecurityMonitoringFailed")); return false; }
            var settings = await NoteArchive.Database.ProtectionSettingsAsync();
            if (!settings.Configured) return await ConfigureAsync();
            var password = new PasswordBox { PlaceholderText = Strings.T("SecurityPassword"), MinWidth = 300 };
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(password);
            content.Children.Add(error);
            var dialog = Dialog("SecurityUnlock", content, "SecurityUnlock");
            if (await WindowsHelloProtection.IsEnabledAsync()) dialog.SecondaryButtonText = Strings.T("SecurityHello");
            var result = false;
            async Task TryUnlock(ContentDialogButtonClickEventArgs args, bool hello)
            {
                var deferral = args.GetDeferral();
                args.Cancel = true;
                dialog.IsPrimaryButtonEnabled = false;
                dialog.IsSecondaryButtonEnabled = false;
                try
                {
                    // Each failed attempt waits a little longer before the next one is accepted.
                    if (failedUnlocks > 0) await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(failedUnlocks, 20) * 500));
                    if (hello) await WindowsHelloProtection.UnlockAsync(Handle);
                    else if (!await NoteArchive.Database.TryUnlockAsync(password.Password))
                    { failedUnlocks++; error.Text = Strings.T("SecurityUnlockFailed"); return; }
                    failedUnlocks = 0;
                    if (generation != epoch) { await NoteArchive.Database.LockAsync(); return; }
                    result = true;
                    Touch();
                    await RefreshSettingsAsync();
                    args.Cancel = false;
                }
                catch { error.Text = Strings.T(hello ? "SecurityHelloFailed" : "SecurityUnlockFailed"); }
                finally
                {
                    password.Password = "";
                    dialog.IsPrimaryButtonEnabled = true;
                    dialog.IsSecondaryButtonEnabled = true;
                    deferral.Complete();
                }
            }
            dialog.PrimaryButtonClick += async (_, args) => await TryUnlock(args, false);
            dialog.SecondaryButtonClick += async (_, args) => await TryUnlock(args, true);
            dialog.Opened += (_, _) => password.Focus(FocusState.Programmatic);
            try { await dialog.ShowAsync(); }
            finally { password.Password = ""; }
            if (result && generation == epoch && !await ReviewIntegrityAsync()) return false;
            if (result && generation == epoch) await window!.Editor.RestoreProtectedDraftAsync();
            if (result && NoteArchive.Database.DamagedNoteIds.Count is > 0 and var damaged)
                await ArchiveDialogs.MessageAsync(Strings.T("SecurityTitle"), Strings.Format(Strings.DefaultMap, "SecurityDamagedNotes", damaged));
            return result && generation == epoch && !window!.Editor.HasProtectedDraft;
        }
        catch { if (window is not null) window.Editor.SaveError = Strings.T("SecurityActionFailed"); return false; }
    }

    /// <summary>
    /// Shown when the unlock found protected records changed outside the app: the user accepts the
    /// archive as it is now or locks it again, for example to restore a backup.
    /// </summary>
    private static async Task<bool> ReviewIntegrityAsync()
    {
        if (NoteArchive.Database.IntegrityReport is not { } report) return true;
        var parts = new List<string>();
        if (report.Unverifiable) parts.Add(Strings.T("SecurityIntegrityUnverifiable"));
        if (report.RolledBack) parts.Add(Strings.T("SecurityIntegrityRolledBack"));
        if (report.NoteIds.Count > 0)
        {
            var titles = new List<string>();
            foreach (var id in report.NoteIds.Take(10))
            {
                var title = (await NoteArchive.Database.SummaryAsync(id))?.Title;
                titles.Add("• " + (string.IsNullOrWhiteSpace(title) ? Strings.T("SecurityIntegrityMissingNote") : title));
            }
            if (report.NoteIds.Count > 10) titles.Add(Strings.Format(Strings.DefaultMap, "SecurityIntegrityMore", report.NoteIds.Count - 10));
            parts.Add(Strings.T("SecurityIntegrityChanged") + "\n" + string.Join("\n", titles));
        }
        parts.Add(Strings.T("SecurityIntegrityAdvice"));
        var dialog = Dialog("SecurityIntegrityTitle",
            new TextBlock { Text = string.Join("\n\n", parts), TextWrapping = TextWrapping.Wrap, MaxWidth = 480 }, "SecurityIntegrityAccept");
        dialog.CloseButtonText = Strings.T("SecurityIntegrityLock");
        dialog.DefaultButton = ContentDialogButton.Close;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                await NoteArchive.Database.AcceptIntegrityAsync();
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticsService.LogError("Security", "Accettazione dello stato dell'archivio non riuscita", ex);
                await FailureAsync();
            }
        }
        await LockAsync();
        return false;
    }

    public static async Task<bool> EnsureNoteAccessAsync(string id)
    {
        if (Blocking) return false;
        try
        {
            var summary = await NoteArchive.Database.SummaryAsync(id);
            return summary is not null && (!summary.IsProtected || await EnsureUnlockedAsync());
        }
        catch { if (window is not null) window.Editor.SaveError = Strings.T("ArchiveOpenFailed"); return false; }
    }

    public static async Task<bool> ConfigureAsync()
    {
        if (!monitorAvailable) { await ArchiveDialogs.MessageAsync(Strings.T("SecurityTitle"), Strings.T("SecurityMonitoringFailed")); return false; }
        var generation = epoch;
        var result = await CredentialsAsync("SecurityConfigure", current: false, recovery: false);
        if (result is null) return false;
        try
        {
            var prepared = await NoteArchive.Database.PrepareCredentialsAsync(CredentialChange.Configure, "", result.Value.New);
            if (prepared is null) return false;
            return await ShowRecoveryCodeAsync(prepared, generation);
        }
        catch { await FailureAsync(); return false; }
    }

    public static async Task ChangePasswordAsync()
    {
        var changed = await CredentialsAsync("SecurityChangePassword", current: true, recovery: false,
            authenticate: (oldPassword, password) => NoteArchive.Database.TryChangePasswordAsync(oldPassword, password));
        if (changed is not null) await RetireHelloAsync();
    }

    // Credential changes rotate the master key, so an existing Windows Hello wrapper can no longer open the archive.
    private static async Task RetireHelloAsync()
    {
        try
        {
            if (!await WindowsHelloProtection.IsEnabledAsync()) return;
            await WindowsHelloProtection.DisableAsync();
        }
        catch (Exception ex) { DiagnosticsService.LogError("Security", "Disattivazione di Windows Hello non riuscita", ex); }
        Changed?.Invoke(null, EventArgs.Empty);
        await ArchiveDialogs.MessageAsync(Strings.T("SecurityTitle"), Strings.T("SecurityHelloRetired"));
    }

    public static async Task RecoverAsync()
    {
        var generation = epoch;
        PreparedCredentials? prepared = null;
        var values = await CredentialsAsync("SecurityRecover", current: false, recovery: true, authenticate: async (code, password) =>
        {
            prepared = await NoteArchive.Database.PrepareCredentialsAsync(CredentialChange.Recover, code, password);
            return prepared is not null;
        });
        if (values is null) { if (prepared is not null) await NoteArchive.Database.CancelCredentialsAsync(prepared); return; }
        try
        {
            if (prepared is null) { await ArchiveDialogs.MessageAsync(Strings.T("SecurityTitle"), Strings.T("SecurityUnlockFailed")); return; }
            if (!await ShowRecoveryCodeAsync(prepared, generation)) return;
            await RetireHelloAsync();
            if (!await ReviewIntegrityAsync()) return;
            await window!.Editor.RestoreProtectedDraftAsync();
        }
        catch { await FailureAsync(); }
    }

    public static async Task RenewRecoveryAsync()
    {
        PreparedCredentials? prepared = null;
        var generation = epoch;
        var password = await PasswordAsync("SecurityRenewRecovery", async value =>
        {
            prepared = await NoteArchive.Database.PrepareCredentialsAsync(CredentialChange.RenewRecovery, value);
            return prepared is not null;
        });
        if (password is null) { if (prepared is not null) await NoteArchive.Database.CancelCredentialsAsync(prepared); return; }
        try
        {
            if (prepared is null) { await ArchiveDialogs.MessageAsync(Strings.T("SecurityTitle"), Strings.T("SecurityUnlockFailed")); return; }
            if (await ShowRecoveryCodeAsync(prepared, generation)) await RetireHelloAsync();
        }
        catch { await FailureAsync(); }
    }

    public static async Task ToggleHelloAsync()
    {
        if (await WindowsHelloProtection.IsEnabledAsync())
        {
            if (await EnsureUnlockedAsync()) await WindowsHelloProtection.DisableAsync();
        }
        else
        {
            await PasswordAsync("SecurityEnableHello", value => WindowsHelloProtection.EnableAsync(value, Handle));
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static async Task ProtectNoteAsync(string id, bool remove = false)
    {
        if (Blocking) return;
        if (!await EnsureUnlockedAsync()) return;
        if (remove && !await ConfirmPlaintextAsync(removing: true)) return;
        if (window!.Editor.CurrentNote?.NoteId == id)
        {
            await window.Editor.CloseEditorAsync();
            if (window.Editor.CurrentNote is not null) return;
        }
        Blocking = true;
        try
        {
            await ClearAttachmentCacheAsync();
            if (remove) await NoteArchive.Database.UnprotectAsync(id);
            else await NoteArchive.Database.ProtectAsync(id);
        }
        catch { await FailureAsync(); return; }
        finally { Blocking = false; NoteArchive.NotifyChanged(); }
        await window.Editor.OpenNoteAsync(id);
    }

    public static Task LockAsync()
    {
        if (locking is not null) return locking;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        locking = completion.Task;
        _ = CompleteLockAsync(completion);
        return completion.Task;
    }
    private static async Task CompleteLockAsync(TaskCompletionSource completion)
    {
        try { await LockCoreAsync(); completion.SetResult(); }
        catch (Exception ex) { completion.SetException(ex); }
        finally { locking = null; }
    }
    // Locking must always complete: keys are wiped even if sealing fails, and the UI is released as soon
    // as no key remains in memory. Text that could not be sealed stays in the editor model, unsaved and
    // hidden, until the next unlock.
    private static async Task LockCoreAsync()
    {
        epoch++;
        Blocking = true;
        operations.Cancel();
        var sealFailed = false;
        try
        {
            CloseDialogs();
            Clearing?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex) { DiagnosticsService.LogError("Security", "Pulizia dell'interfaccia al blocco non riuscita", ex); }

        try { await window!.Editor.SealAndCloseProtectedAsync(); }
        catch (Exception ex)
        {
            sealFailed = true;
            DiagnosticsService.LogError("Security", "Sigillo della bozza protetta non riuscito", ex);
        }

        try { await ClearAttachmentCacheAsync(); }
        catch (Exception ex) { DiagnosticsService.LogError("Security", "Cache degli allegati non svuotata", ex); }

        try { await NoteArchive.Database.LockAsync(); }
        catch (Exception ex) { DiagnosticsService.LogError("Security", "Blocco delle note fallito", ex); }
        finally
        {
            Blocking = NoteArchive.Database.IsUnlocked;
            if (!Blocking) operations = new();
            if (sealFailed) window!.Editor.SaveError = Strings.T("SecurityPendingSave");
            NoteArchive.NotifyChanged();
            Changed?.Invoke(null, EventArgs.Empty);
        }
        if (!Blocking) _ = Task.Run(Interop.ProcessHardening.ReleaseFreedMemory);
    }

    public static async Task<bool> AuthorizeExportAsync(IEnumerable<string> ids)
    {
        if (Blocking) return false;
        var protectedContent = false;
        foreach (var id in ids)
        {
            if (!await EnsureNoteAccessAsync(id)) return false;
            protectedContent |= await NoteArchive.Database.HasProtectedContentAsync(id);
        }
        return !protectedContent || await ConfirmPlaintextAsync();
    }

    private static (string? NoteId, long Session) copyConsent;

    /// <summary>Asks once per protected note and unlock session before its text goes to the clipboard.</summary>
    public static async Task<bool> ConfirmCopyAsync(NoteEditorViewModel? tab)
    {
        if (tab?.Metadata?.IsProtected != true || tab.NoteId is not { } id) return true;
        if (Blocking) return false;
        var session = NoteArchive.Database.SessionVersion;
        if (copyConsent == (id, session)) return true;
        if (!await ConfirmPlaintextAsync()) return false;
        copyConsent = (id, session);
        return true;
    }

    public static async Task<bool> ConfirmPlaintextAsync(bool removing = false) => await Dialog(removing ? "SecurityUnprotect" : "SecurityPlaintextTitle",
        new TextBlock { Text = Strings.T(removing ? "SecurityUnprotectBody" : "SecurityPlaintextBody"), TextWrapping = TextWrapping.Wrap, MaxWidth = 460 }, removing ? "SecurityUnprotect" : "SecurityContinue").ShowAsync() == ContentDialogResult.Primary;

    public static async Task ApplySettingsAsync(bool hidden, int minutes)
    {
        if (!await EnsureUnlockedAsync()) return;
        await ClearAttachmentCacheAsync();
        await NoteArchive.Database.SetProtectionSettingsAsync(hidden, minutes);
        idleMinutes = minutes;
        NoteArchive.NotifyChanged();
    }

    public static async Task RetryMaintenanceAsync()
    {
        await ClearAttachmentCacheAsync();
        await NoteArchive.Database.CompleteProtectionMaintenanceAsync();
        NoteArchive.NotifyChanged();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    internal static async Task ClearAttachmentCacheAsync()
    {
        await ArchiveAssets.CacheGate.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                var folder = Path.GetFullPath(Path.Combine(AppPaths.Root, "attachment-cache"));
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.EnumerateFiles(folder)) File.Delete(file);
            });
        }
        finally { ArchiveAssets.CacheGate.Release(); }
    }

    private static ContentDialog Dialog(string title, object content, string primary) => new()
    {
        Title = Strings.T(title), Content = content, XamlRoot = root!.XamlRoot,
        RequestedTheme = root.ActualTheme, PrimaryButtonText = Strings.T(primary), CloseButtonText = Strings.T("Cancel")
    };
    private static Task FailureAsync() => ArchiveDialogs.MessageAsync(Strings.T("SecurityTitle"), Strings.T("SecurityActionFailed"));
    private static async Task<bool> ShowRecoveryCodeAsync(PreparedCredentials prepared, long generation)
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 480 };
        panel.Children.Add(new TextBlock { Text = Strings.T("RecoveryDownloadDescription"), TextWrapping = TextWrapping.Wrap });
        var value = new TextBox { Text = prepared.RecoveryCode, IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(value); panel.Children.Add(error);
        var dialog = Dialog("SecurityRecoveryCode", panel, "SecurityCodeSaved");
        dialog.SecondaryButtonText = Strings.T("RecoveryDownload");
        dialog.IsPrimaryButtonEnabled = false;
        var committed = false;
        var downloaded = false;
        dialog.SecondaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral(); args.Cancel = true;
            dialog.IsSecondaryButtonEnabled = false;
            try
            {
                var file = await FilePickerService.SaveRecoveryAsync(RecoveryDocument.FileName(prepared));
                if (file is null || generation != epoch) return;
                await RecoveryDocument.WriteAsync(file.Path, prepared, OperationsToken);
                if (generation != epoch) return;
                downloaded = true; error.Text = Strings.T("RecoveryDownloaded");
                dialog.IsPrimaryButtonEnabled = true;
            }
            catch (OperationCanceledException) { }
            catch { error.Text = Strings.T("RecoveryDownloadFailed"); }
            finally { dialog.IsSecondaryButtonEnabled = true; deferral.Complete(); }
        };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral(); args.Cancel = true;
            dialog.IsPrimaryButtonEnabled = dialog.IsSecondaryButtonEnabled = false;
            try
            {
                if (!downloaded || generation != epoch) return;
                await NoteArchive.Database.CommitCredentialsAsync(prepared);
                committed = true; Touch(); args.Cancel = false;
            }
            catch { error.Text = Strings.T("SecurityActionFailed"); }
            finally
            {
                dialog.IsPrimaryButtonEnabled = downloaded && generation == epoch;
                dialog.IsSecondaryButtonEnabled = true; deferral.Complete();
            }
        };
        try { if (generation == epoch) await dialog.ShowAsync(); return committed; }
        finally { value.Text = ""; await NoteArchive.Database.CancelCredentialsAsync(prepared); }
    }
    private static async Task<string?> PasswordAsync(string title, Func<string, Task<bool>>? authenticate = null)
    {
        var box = new PasswordBox { PlaceholderText = Strings.T("SecurityPassword"), MinWidth = 300 };
        var panel = new StackPanel { Spacing = 12 };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(box); panel.Children.Add(error);
        var dialog = Dialog(title, panel, "SecurityContinue");
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            if (authenticate is null) return;
            var deferral = args.GetDeferral(); args.Cancel = true; dialog.IsPrimaryButtonEnabled = false;
            try { if (await authenticate(box.Password)) args.Cancel = false; else error.Text = Strings.T("SecurityUnlockFailed"); }
            catch { error.Text = Strings.T("SecurityActionFailed"); }
            finally { dialog.IsPrimaryButtonEnabled = true; deferral.Complete(); }
        };
        dialog.Opened += (_, _) => box.Focus(FocusState.Programmatic);
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Password : null; }
        finally { box.Password = ""; }
    }
    private static async Task<(string Current, string New)?> CredentialsAsync(string title, bool current, bool recovery, Func<string, string, Task<bool>>? authenticate = null)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 320, MaxWidth = 480 };
        var old = new PasswordBox { PlaceholderText = Strings.T("SecurityCurrentPassword") };
        var code = new PasswordBox { PlaceholderText = Strings.T("SecurityRecoveryCode") };
        var password = new PasswordBox { PlaceholderText = Strings.T("SecurityNewPassword") };
        var confirm = new PasswordBox { PlaceholderText = Strings.T("SecurityConfirmPassword") };
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        if (current) panel.Children.Add(old);
        if (recovery) panel.Children.Add(code);
        panel.Children.Add(password); panel.Children.Add(confirm); panel.Children.Add(error);
        var dialog = Dialog(title, panel, "SecurityContinue");
        dialog.PrimaryButtonClick += async (_, e) =>
        {
            if (!PasswordPolicy.IsAcceptable(password.Password) || password.Password != confirm.Password)
            { e.Cancel = true; error.Text = Strings.T("SecurityPasswordRules"); return; }
            if (authenticate is null) return;
            var deferral = e.GetDeferral(); e.Cancel = true; dialog.IsPrimaryButtonEnabled = false;
            try
            {
                if (await authenticate(recovery ? code.Password : old.Password, password.Password)) e.Cancel = false;
                else error.Text = Strings.T("SecurityUnlockFailed");
            }
            catch { error.Text = Strings.T("SecurityActionFailed"); }
            finally { dialog.IsPrimaryButtonEnabled = true; deferral.Complete(); }
        };
        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary ? (recovery ? code.Password : old.Password, password.Password) : null;
        }
        finally { old.Password = ""; code.Password = ""; password.Password = ""; confirm.Password = ""; }
    }

    private static void CloseDialogs()
    {
        if (root?.XamlRoot is not { } xamlRoot) return;
        void Hide(DependencyObject value)
        {
            if (value is ContentDialog dialog) { dialog.Hide(); return; }
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++) Hide(VisualTreeHelper.GetChild(value, i));
        }
        foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
        { if (popup.Child is not null) Hide(popup.Child); popup.IsOpen = false; }
    }
}

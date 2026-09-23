using Velopack;
using Velopack.Sources;

namespace MarkdownMkII.Services;

public enum UpdateState { Unsupported, Idle, Checking, UpToDate, Ready, Failed }

/// <summary>
/// Updates for copies installed with the GitHub setup (Velopack). A newer release is downloaded in the
/// background and applied only after a normal close, when notes are saved and locked.
/// </summary>
public static class AppUpdates
{
    private static readonly Lazy<UpdateManager?> Manager = new(Create);
    private static UpdateInfo? ready;
    private static bool restartAfterUpdate;

    public static UpdateState State { get; private set; } = UpdateState.Idle;
    public static string? ReadyVersion => ready?.TargetFullRelease.Version.ToString();
    public static event EventHandler? Changed;

    /// <summary>MSIX and development builds are not managed by Velopack and never update themselves.</summary>
    public static bool IsSupported => Manager.Value?.IsInstalled == true;

    /// <summary>How this copy was installed, for diagnostics.</summary>
    public static string InstallKind => Manager.Value is { IsInstalled: true } manager
        ? manager.IsPortable ? "portable (Velopack)" : "setup (Velopack)"
        : "not managed by Velopack";

    private static UpdateManager? Create()
    {
        try { return new UpdateManager(new GithubSource(AppLinks.Repository.ToString(), null, false)); }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Updates", "Gestore degli aggiornamenti non disponibile", ex);
            return null;
        }
    }

    public static Task CheckAtStartupAsync()
        => SettingsService.Instance.Diagnostics.CheckForUpdates ? CheckAsync() : Task.CompletedTask;

    public static async Task CheckAsync()
    {
        if (!IsSupported) { SetState(UpdateState.Unsupported); return; }
        if (State is UpdateState.Checking or UpdateState.Ready) return;
        SetState(UpdateState.Checking);
        try
        {
            var manager = Manager.Value!;
            var update = await manager.CheckForUpdatesAsync();
            if (update is null) { SetState(UpdateState.UpToDate); return; }
            await manager.DownloadUpdatesAsync(update);
            ready = update;
            SetState(UpdateState.Ready);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Updates", "Controllo degli aggiornamenti non riuscito", ex);
            SetState(UpdateState.Failed);
        }
    }

    /// <summary>Called by the window once closing is confirmed: Velopack waits for the process to exit.</summary>
    public static void ApplyOnExit()
    {
        if (ready is null || Manager.Value is not { } manager) return;
        try { manager.WaitExitThenApplyUpdates(ready.TargetFullRelease, silent: true, restart: restartAfterUpdate); }
        catch (Exception ex) { DiagnosticsService.LogError("Updates", "Installazione dell'aggiornamento non avviata", ex); }
    }

    /// <summary>Closes through the normal flow (saving and locking), then installs and starts the new version.</summary>
    public static void RestartNow()
    {
        if (ready is null || App.MainWindow is not MainWindow window) return;
        restartAfterUpdate = true;
        window.RequestClose();
    }

    private static void SetState(UpdateState state)
    {
        State = state;
        Changed?.Invoke(null, EventArgs.Empty);
    }
}

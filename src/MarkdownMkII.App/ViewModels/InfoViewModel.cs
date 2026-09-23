using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Windows.ApplicationModel.DataTransfer;
using System.Runtime.InteropServices;

namespace MarkdownMkII.ViewModels;

public partial class InfoViewModel : ObservableObject
{
    public string DatabasePath => NoteArchive.Database.FilePath;
    public string SettingsPath { get; } = SettingsService.FilePath;
    public string LogPath => DiagnosticsService.LogPath;
    public string VersionSummary => $"{DiagnosticsReport.AppVersion} · {DiagnosticsReport.Commit} · {RuntimeInformation.ProcessArchitecture}";
    public string Platform => RuntimeInformation.OSDescription;
    public string Runtime => RuntimeInformation.FrameworkDescription;
    public string WinUIVersion => typeof(Microsoft.UI.Xaml.Application).Assembly.GetName().Version?.ToString() ?? string.Empty;
    public string ProjectSummary => Strings.Format(Strings.DefaultMap, "InfoProjectSummary", AppLinks.Author, AppLinks.License);

    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    public InfoViewModel()
    {
        AppUpdates.Changed += (_, _) => dispatcher.TryEnqueue(() =>
        {
            OnPropertyChanged(nameof(UpdateStatus));
            OnPropertyChanged(nameof(IsUpdateReady));
            OnPropertyChanged(nameof(CanCheckForUpdates));
        });
    }

    public bool UpdatesSupported => AppUpdates.IsSupported;
    public bool IsUpdateReady => AppUpdates.State == UpdateState.Ready;
    public bool CanCheckForUpdates => AppUpdates.IsSupported && AppUpdates.State is not (UpdateState.Checking or UpdateState.Ready);
    public string UpdateStatus => !AppUpdates.IsSupported ? Strings.T("InfoUpdatesUnsupported") : AppUpdates.State switch
    {
        UpdateState.Checking => Strings.T("InfoUpdatesChecking"),
        UpdateState.UpToDate => Strings.Format(Strings.DefaultMap, "InfoUpdatesUpToDate", DiagnosticsReport.AppVersion),
        UpdateState.Ready => Strings.Format(Strings.DefaultMap, "InfoUpdatesReady", AppUpdates.ReadyVersion ?? ""),
        UpdateState.Failed => Strings.T("InfoUpdatesFailed"),
        _ => Strings.Format(Strings.DefaultMap, "InfoUpdatesIdle", DiagnosticsReport.AppVersion)
    };

    public bool CheckForUpdatesAtStartup
    {
        get => SettingsService.Instance.Diagnostics.CheckForUpdates;
        set
        {
            if (value == CheckForUpdatesAtStartup) return;
            _ = SettingsService.Instance.UpdateAsync(Core.Models.SettingsArea.Diagnostics, s => s.Diagnostics.CheckForUpdates = value);
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private Task CheckForUpdatesAsync() => AppUpdates.CheckAsync();

    [RelayCommand]
    private void RestartToUpdate() => AppUpdates.RestartNow();

    private bool diagnosticsCopied;
    public bool DiagnosticsCopied
    {
        get => diagnosticsCopied;
        set => SetProperty(ref diagnosticsCopied, value);
    }

    [RelayCommand]
    private async Task CopyDiagnosticsAsync()
    {
        try
        {
            CopyText(await DiagnosticsReport.BuildAsync());
            DiagnosticsCopied = true;
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Info", "Copia delle informazioni di diagnostica fallita", ex);
        }
    }

    // The full report also goes to the clipboard: GitHub trims long prefilled bodies.
    [RelayCommand]
    private async Task ReportIssueAsync()
    {
        try
        {
            var report = await DiagnosticsReport.BuildAsync();
            CopyText(report);
            DiagnosticsCopied = true;
            await Windows.System.Launcher.LaunchUriAsync(AppLinks.NewIssue(Strings.Format(Strings.DefaultMap, "InfoIssueTemplate", report)));
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Info", "Apertura della segnalazione fallita", ex);
        }
    }

    [RelayCommand]
    private async Task OpenRepositoryAsync() => await Windows.System.Launcher.LaunchUriAsync(AppLinks.Repository);

    [RelayCommand]
    private async Task OpenLicenseAsync() => await Windows.System.Launcher.LaunchUriAsync(AppLinks.LicenseText);

    [RelayCommand]
    private async Task OpenLogAsync()
    {
        try
        {
            await LocalFileLauncher.EnsureTextFileAndOpenAsync(LogPath);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Info", "Apertura log fallita", ex);
        }
    }

    private static void CopyText(string text)
    {
        var data = new DataPackage();
        data.SetText(text);
        Clipboard.SetContent(data);
        Clipboard.Flush();
    }
}

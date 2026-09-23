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
    public string VersionSummary => $"{DiagnosticsReport.AppVersion} · {RuntimeInformation.ProcessArchitecture}";
    public string Platform => RuntimeInformation.OSDescription;
    public string Runtime => RuntimeInformation.FrameworkDescription;
    public string WinUIVersion => typeof(Microsoft.UI.Xaml.Application).Assembly.GetName().Version?.ToString() ?? string.Empty;
    public string ProjectSummary => Strings.Format(Strings.DefaultMap, "InfoProjectSummary", AppLinks.Author, AppLinks.License);

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

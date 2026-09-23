using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using System.Runtime.InteropServices;

namespace MarkdownMkII.ViewModels;

public partial class InfoViewModel : ObservableObject
{
    public string DatabasePath => NoteArchive.Database.FilePath;
    public string Version { get; }

    public string SettingsPath { get; }
    public string VersionSummary => $"{Version} · {RuntimeInformation.ProcessArchitecture}";
    public string Platform => RuntimeInformation.OSDescription;
    public string Runtime => RuntimeInformation.FrameworkDescription;
    public string WinUIVersion => typeof(Microsoft.UI.Xaml.Application).Assembly.GetName().Version?.ToString() ?? string.Empty;

    private bool diagnosticsCopied;
    public bool DiagnosticsCopied
    {
        get => diagnosticsCopied;
        set => SetProperty(ref diagnosticsCopied, value);
    }

    public string LogPath => DiagnosticsService.LogPath;


    public InfoViewModel()
    {
        try
        {
            var package = Package.Current;
            Version = $"{package.Id.Version.Major}.{package.Id.Version.Minor}.{package.Id.Version.Build}.{package.Id.Version.Revision}";
        }
        catch
        {
            Version = typeof(InfoViewModel).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        }

        SettingsPath = SettingsService.FilePath;
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        try
        {
            var text = string.Join(Environment.NewLine,
                $"Markdown MkII {VersionSummary}", Platform, Runtime, $"WinUI 3: {WinUIVersion}",
                $"{Strings.T("InfoSettingsPath.Header")}: {SettingsPath}",
                $"{Strings.T("InfoDatabasePath.Header")}: {DatabasePath}",
                $"{Strings.T("InfoLogPath.Header")}: {LogPath}");
            var data = new DataPackage();
            data.SetText(text);
            Clipboard.SetContent(data);
            Clipboard.Flush();
            DiagnosticsCopied = true;
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Info", "Copia delle informazioni di diagnostica fallita", ex);
        }
    }

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
}

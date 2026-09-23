using System.IO.Compression;
using System.Text.Json;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownMkII.Services;
public static class ArchiveBackup
{
    public static async Task CreateAsync()
    {
        if (await TryCreateAsync())
            await ArchiveDialogs.MessageAsync(Strings.T("ArchiveBackupTitle"), Strings.T("ArchiveBackupDone"));
    }

    /// <summary>Saves a backup where the user chooses; false when unsaved edits block it or the picker is cancelled.</summary>
    public static async Task<bool> TryCreateAsync()
    {
        var editor = ((MainWindow)App.MainWindow!).Editor;
        if (!await editor.FlushAsync(true))
            return false;
        var destination = await FilePickerService.SaveBackupAsync();
        if (destination is null)
            return false;
        await CreateAtAsync(destination.Path);
        return true;
    }

    private static async Task CreateAtAsync(string destination)
    {
        await SettingsService.Instance.FlushAsync();
        var temp = Path.Combine(AppPaths.Root, "backup-" + Guid.NewGuid().ToString("N") + ".db");
        var outputPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var preferences = JsonSerializer.Serialize(SettingsService.Instance.Current, SettingsService.JsonOptions);
        try
        {
            await NoteArchive.Database.BackupAsync(temp);
            await Task.Run(() =>
            {
                using var output = File.Create(outputPath);
                using var zip = new ZipArchive(output, ZipArchiveMode.Create);
                zip.CreateEntryFromFile(temp, "notes.db", CompressionLevel.Fastest);
                using var writer = new StreamWriter(zip.CreateEntry("settings.json", CompressionLevel.Fastest).Open());
                writer.Write(preferences);
            });
            File.Move(outputPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
    }

    public static async Task RestoreAsync()
    {
        var source = await FilePickerService.OpenBackupAsync();
        if (source is null)
            return;
        var root = (FrameworkElement)App.MainWindow!.Content;
        var dialog = new ContentDialog
        {
            Title = Strings.T("ArchiveRestoreBackup"),
            Content = Strings.T("ArchiveRestoreDescription"),
            PrimaryButtonText = Strings.T("ArchiveRestoreBackup"),
            CloseButtonText = Strings.T("Cancel"),
            XamlRoot = root.XamlRoot,
            RequestedTheme = root.ActualTheme
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;
        var editor = ((MainWindow)App.MainWindow!).Editor;
        if (!await editor.FlushAsync(true))
            return;
        var database = Path.Combine(AppPaths.Root, "restore-" + Guid.NewGuid().ToString("N") + ".db");
        var rollback = database + ".previous";
        var previousSettings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(SettingsService.Instance.Current, SettingsService.JsonOptions), SettingsService.JsonOptions)!;
        var replaced = false;
        try
        {
            AppSettings settings;
            using (var zip = ZipFile.OpenRead(source.Path))
            {
                var archive = zip.GetEntry("notes.db") ?? throw new InvalidDataException("notes.db");
                var preferences = zip.GetEntry("settings.json") ?? throw new InvalidDataException("settings.json");
                using (var input = preferences.Open())
                    settings = await JsonSerializer.DeserializeAsync<AppSettings>(input, SettingsService.JsonOptions) ?? throw new InvalidDataException("settings.json");
                await Task.Run(() => archive.ExtractToFile(database));
            }

            var safety = Path.Combine(AppPaths.Root, "before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6] + ".mkii-backup");
            await CreateAtAsync(safety);
            await editor.CloseEditorAsync();
            if (editor.CurrentNote is not null)
                return;
            await NoteArchive.Database.BackupAsync(rollback);
            await NoteSecurity.LockAsync();
            replaced = true;
            await NoteArchive.Database.RestoreAsync(database);
            await SettingsService.Instance.RestorePreferencesAsync(settings);
            await WindowsHelloProtection.DisableAsync();
            await NoteSecurity.RefreshSettingsAsync(notify: true);
            NoteArchive.NotifyChanged();
            await ArchiveDialogs.MessageAsync(Strings.T("ArchiveRestoreBackup"), Strings.T("ArchiveRestoreDone"));
        }
        catch (Exception ex)
        {
            if (replaced)
            {
                await NoteArchive.Database.RestoreAsync(rollback);
                await SettingsService.Instance.RestorePreferencesAsync(previousSettings);
                await NoteSecurity.RefreshSettingsAsync(notify: true);
                NoteArchive.NotifyChanged();
            }

            DiagnosticsService.LogError("Archive", "Ripristino archivio fallito", ex);
            await ArchiveDialogs.MessageAsync(Strings.T("ArchiveActionFailed"), ex.Message);
        }
        finally
        {
            if (File.Exists(database))
                File.Delete(database);
            if (File.Exists(rollback))
                File.Delete(rollback);
        }
    }
}

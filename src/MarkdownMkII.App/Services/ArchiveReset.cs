using MarkdownMkII.Services.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownMkII.Services;

/// <summary>Erases every note, attachment, credential and preference, returning the app to its first-run state.</summary>
public static class ArchiveReset
{
    private static readonly string[] Files = ["diagnostics.log", "sealed-draft.json", "sealed-draft.json.tmp"];
    private static readonly string[] Folders = ["attachment-cache", "recovery", "history"];
    private static readonly string[] Patterns = ["backup-*.db", "restore-*.db", "restore-*.db.previous", "before-restore-*.mkii-backup", "*.mkii-backup.*.tmp"];

    public static async Task RunAsync()
    {
        if (await ConfirmAsync() is not { } backupFirst)
            return;
        if (backupFirst && !await TryBackupAsync())
            return;

        var editor = ((MainWindow)App.MainWindow!).Editor;
        try
        {
            await editor.DiscardForResetAsync();
            await WindowsHelloProtection.DisableAsync();
            await NoteSecurity.ClearAttachmentCacheAsync();
                await NoteArchive.Database.ResetAsync();
                var leftovers = await Task.Run(DeleteAppFiles);
                try { Interop.IntegrityAnchorStore.ForgetAll(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { leftovers.Add("Integrity"); }
            await SettingsService.Instance.ResetAsync();
            await NoteSecurity.RefreshSettingsAsync(notify: true);
            NoteArchive.NotifyChanged();
            (App.MainWindow as MainWindow)?.ShowEmptyWorkspace();
            var message = Strings.T("FactoryResetDone");
            if (leftovers.Count > 0) message += "\n\n" + Strings.T("FactoryResetLeftovers") + "\n" + string.Join("\n", leftovers.Take(10));
            await ArchiveDialogs.MessageAsync(Strings.T("FactoryResetTitle"), message);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Reset completo non riuscito", ex);
            await ArchiveDialogs.MessageAsync(Strings.T("FactoryResetTitle"), Strings.T("FactoryResetFailed") + "\n\n" + ex.Message);
        }
    }

    /// <returns>Null when cancelled; otherwise whether a backup must be saved first.</returns>
    private static async Task<bool?> ConfirmAsync()
    {
        var root = (FrameworkElement)App.MainWindow!.Content;
        var word = Strings.T("FactoryResetConfirmWord");
        var panel = new StackPanel { Spacing = 12, MaxWidth = 460 };
        panel.Children.Add(new TextBlock { Text = Strings.T("FactoryResetBody"), TextWrapping = TextWrapping.Wrap });
        var backup = new CheckBox { Content = Strings.T("FactoryResetBackup"), IsChecked = true };
        panel.Children.Add(backup);
        var confirm = new TextBox { Header = Strings.Format(Strings.DefaultMap, "FactoryResetConfirmHint", word), PlaceholderText = word };
        panel.Children.Add(confirm);
        var dialog = new ContentDialog
        {
            Title = Strings.T("FactoryResetTitle"),
            Content = panel,
            PrimaryButtonText = Strings.T("FactoryResetButton.Content"),
            CloseButtonText = Strings.T("Cancel"),
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = root.XamlRoot,
            RequestedTheme = root.ActualTheme
        };
        confirm.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = string.Equals(confirm.Text.Trim(), word, StringComparison.CurrentCultureIgnoreCase);
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? backup.IsChecked == true : null;
    }

    private static async Task<bool> TryBackupAsync()
    {
        try
        {
            if (await ArchiveBackup.TryCreateAsync()) return true;
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Backup prima del reset non riuscito", ex);
        }

        await ArchiveDialogs.MessageAsync(Strings.T("FactoryResetTitle"), Strings.T("FactoryResetNoBackup"));
        return false;
    }

    /// <summary>Deletes the app's own files beside the archive; returns the names that could not be removed.</summary>
    private static List<string> DeleteAppFiles()
    {
        var root = AppPaths.Root;
        var failed = new List<string>();
        void Try(string name, Action delete)
        {
            try { delete(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failed.Add(name); }
        }

        foreach (var name in Files)
        {
            var file = Path.Combine(root, name);
            if (File.Exists(file)) Try(name, () => File.Delete(file));
        }

        foreach (var name in Folders)
        {
            var folder = Path.Combine(root, name);
            if (Directory.Exists(folder)) Try(name, () => Directory.Delete(folder, recursive: true));
        }

        if (Directory.Exists(root))
            foreach (var pattern in Patterns)
                foreach (var file in Directory.EnumerateFiles(root, pattern))
                    Try(Path.GetFileName(file), () => File.Delete(file));
        return failed;
    }
}

using Windows.Storage;
using Windows.System;

namespace MarkdownMkII.Services;

public static class LocalFileLauncher
{
    public static void RevealInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var existsFile = File.Exists(path);
        var existsDir = Directory.Exists(path);
        if (!existsFile && !existsDir)
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = existsDir ? path : "/select,\"" + path + "\"",
            UseShellExecute = true
        });
    }

    public static async Task OpenAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        await Launcher.LaunchFileAsync(file);
    }

    public static async Task EnsureTextFileAndOpenAsync(string path)
    {
        if (!File.Exists(path))
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, string.Empty);
        }

        await OpenAsync(path);
    }
}

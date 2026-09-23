using Windows.Storage;

namespace MarkdownMkII.Services;

public static class AppPaths
{
    public static string Root
    {
        get
        {
#if DEBUG
            // Keep manual UI verification separate from the user's notes and settings.
            var developmentRoot = Environment.GetEnvironmentVariable("MARKDOWN_MKII_DATA_DIR");
            // MSIX F5 activation does not propagate launchSettings environment variables.
            // A local, ignored marker also supports isolated debugger verification.
            if (string.IsNullOrWhiteSpace(developmentRoot))
                for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                {
                    if (!File.Exists(Path.Combine(directory.FullName, "MarkdownMkII.slnx"))) continue;
                    var marker = Path.Combine(directory.FullName, ".artifacts", "debug-data-root.txt");
                    if (File.Exists(marker)) developmentRoot = File.ReadAllText(marker).Trim();
                    break;
                }
            if (!string.IsNullOrWhiteSpace(developmentRoot) && Path.IsPathFullyQualified(developmentRoot))
                return developmentRoot;
#endif
            try
            {
                return ApplicationData.Current.LocalFolder.Path;
            }
            catch
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Markdown MkII");
            }
        }
    }

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string LogFile => Path.Combine(Root, "diagnostics.log");


}

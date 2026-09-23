// The linked ViewModel uses the real parser and MVVM generator. These adapters
// provide paths, diagnostics and localization without loading the Windows UI runtime.
namespace MarkdownMkII.Services
{
    internal static class AppPaths
    {
        public static string SettingsFile { get; } = Path.Combine(Path.GetTempPath(), "mkii-app-tests-" + Guid.NewGuid().ToString("N"), "settings.json");
    }

    internal static class DiagnosticsService
    {
        public static void LogError(string stage, string message, Exception? exception = null) { }
    }
}

namespace MarkdownMkII.Services.Localization
{
    internal static class Strings
    {
        public static string T(string key) => key;
    }
}

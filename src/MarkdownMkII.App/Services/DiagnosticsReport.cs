using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using MarkdownMkII.Storage;
using Microsoft.Win32;

namespace MarkdownMkII.Services;

/// <summary>
/// Markdown report to paste into a GitHub issue. It describes the app, Windows, the archive and recent
/// errors; it never includes note text, titles, passwords or keys, and hides the Windows user name.
/// </summary>
public static class DiagnosticsReport
{
    private const int RecentErrors = 20;

    // InformationalVersion is "1.0.42+<commit>" in releases and "1.0.0-dev+<commit>" in local builds.
    private static readonly string[] InformationalVersion = (typeof(DiagnosticsReport).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "0.0.0")
        .Split('+', 2);

    public static string AppVersion { get; } = InformationalVersion[0];
    public static string Commit { get; } = InformationalVersion.Length > 1 ? InformationalVersion[1][..Math.Min(7, InformationalVersion[1].Length)] : "unknown";
    public static bool IsPackaged { get; } = ReadIsPackaged();

    public static async Task<string> BuildAsync()
    {
        var text = new StringBuilder();
        text.AppendLine("### Markdown MkII diagnostics").AppendLine();
        text.AppendLine("| | |").AppendLine("| --- | --- |");
        void Row(string name, string value) => text.AppendLine($"| {name} | {value.Replace("|", "\\|")} |");

        Row("App", $"{AppVersion} · commit {Commit} · {RuntimeInformation.ProcessArchitecture} · {(IsPackaged ? "MSIX" : AppUpdates.InstallKind)} · {Configuration}");
        Row("Updates", AppUpdates.IsSupported ? $"{AppUpdates.State} · check at startup: {YesNo(SettingsService.Instance.Diagnostics.CheckForUpdates)}" : "n/a");
        Row("Windows", WindowsVersion());
        Row(".NET", RuntimeInformation.FrameworkDescription);
        Row("Windows App SDK", WindowsAppSdkVersion());
        Row("Language", Language());
        Row("Theme", SettingsService.Instance.Appearance.Theme.ToString());
        Row("Process", ProcessInfo());

        try
        {
            var archive = await NoteArchive.Database.DiagnosticsAsync();
            var hello = archive.ProtectionConfigured && await SafeAsync(WindowsHelloProtection.IsEnabledAsync);
            Row("Archive", $"schema {archive.SchemaVersion} · SQLite {archive.SqliteVersion} · {archive.JournalMode} · {Size(archive.DatabaseBytes)} + WAL {Size(archive.WalBytes)}");
            Row("Notes", $"{archive.Notes} active · {archive.ArchivedNotes} archived · {archive.TrashedNotes} in trash · {archive.ProtectedNotes} protected · {archive.Revisions} revisions");
            Row("Attachments", $"{archive.Attachments} · {Size(archive.AttachmentBytes)}");
            Row("Protection", archive.ProtectionConfigured
                ? $"configured · {(archive.Unlocked ? "unlocked" : "locked")} · details hidden: {YesNo(archive.HideDetails)} · Windows Hello: {YesNo(hello)} · cleanup pending: {YesNo(archive.MaintenancePending)} · integrity: {(archive.IntegrityIssue ? "report pending" : "ok")}"
                : "not configured");
        }
        catch (Exception ex)
        {
            Row("Archive", "unavailable: " + ex.GetType().Name);
        }

        Row("Log level", SettingsService.Instance.Diagnostics.LogLevel.ToString());
        Row("Data folder", Anonymize(AppPaths.Root));

        var errors = await Task.Run(ReadRecentErrors);
        text.AppendLine().AppendLine($"<details><summary>Recent errors ({errors.Count})</summary>").AppendLine();
        text.AppendLine("```");
        foreach (var line in errors) text.AppendLine(line);
        if (errors.Count == 0) text.AppendLine("none");
        text.AppendLine("```").AppendLine().AppendLine("</details>");
        return text.ToString();
    }

    private static string Configuration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    private static bool ReadIsPackaged()
    {
        try { return Windows.ApplicationModel.Package.Current is not null; }
        catch (Exception ex) when (ex is InvalidOperationException or COMException) { return false; }
    }

    private static string WindowsVersion()
    {
        var version = Environment.OSVersion.Version;
        var name = version.Build >= 22000 ? "Windows 11" : "Windows 10";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var display = key?.GetValue("DisplayVersion") as string;
            var revision = key?.GetValue("UBR") is int ubr ? "." + ubr : "";
            return $"{name} {display} · build {version.Build}{revision} · {RuntimeInformation.OSArchitecture}".Replace("  ", " ");
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return $"{name} · build {version.Build} · {RuntimeInformation.OSArchitecture}";
        }
    }

    private static string WindowsAppSdkVersion()
    {
        try { return Microsoft.Windows.ApplicationModel.WindowsAppRuntime.RuntimeInfo.AsString; }
        catch (Exception ex) when (ex is COMException or TypeLoadException or DllNotFoundException)
        {
            return typeof(Microsoft.UI.Xaml.Application).Assembly.GetName().Version?.ToString() ?? "unknown";
        }
    }

    private static string Language()
    {
        var setting = SettingsService.Instance.Appearance.Language;
        var active = Microsoft.Windows.Globalization.ApplicationLanguages.Languages.FirstOrDefault() ?? "?";
        var system = string.Join(", ", Windows.System.UserProfile.GlobalizationPreferences.Languages);
        return $"app {active} (setting: {(setting.Length == 0 ? "system" : setting)}) · Windows: {system} · format {CultureInfo.CurrentCulture.Name}";
    }

    private static string ProcessInfo()
    {
        using var process = Process.GetCurrentProcess();
        var uptime = DateTime.Now - process.StartTime;
        return $"running {(int)uptime.TotalHours:00}:{uptime.Minutes:00}:{uptime.Seconds:00} · memory {Size(process.WorkingSet64)}";
    }

    private static List<string> ReadRecentErrors()
    {
        try
        {
            if (!File.Exists(DiagnosticsService.LogPath)) return [];
            using var stream = new FileStream(DiagnosticsService.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var errors = new Queue<string>();
            while (reader.ReadLine() is { } line)
            {
                if (!line.Contains("| ERROR", StringComparison.Ordinal)) continue;
                errors.Enqueue(Anonymize(line));
                if (errors.Count > RecentErrors) errors.Dequeue();
            }
            return [.. errors];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ["log unreadable: " + ex.GetType().Name];
        }
    }

    // Paths inside the profile reveal the Windows user name; keep them recognizable but anonymous.
    private static string Anonymize(string value)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length > 0) value = value.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        var user = Environment.UserName;
        return user.Length > 2 ? value.Replace(user, "<user>", StringComparison.OrdinalIgnoreCase) : value;
    }

    private static async Task<bool> SafeAsync(Func<Task<bool>> check)
    {
        try { return await check(); }
        catch (Exception ex) when (ex is COMException or System.Security.Cryptography.CryptographicException or IOException or UnauthorizedAccessException) { return false; }
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string Size(long bytes) => bytes switch
    {
        < 1024 => FormattableString.Invariant($"{bytes} B"),
        < 1024 * 1024 => FormattableString.Invariant($"{bytes / 1024.0:0.#} KB"),
        < 1024L * 1024 * 1024 => FormattableString.Invariant($"{bytes / (1024.0 * 1024):0.#} MB"),
        _ => FormattableString.Invariant($"{bytes / (1024.0 * 1024 * 1024):0.##} GB")
    };
}

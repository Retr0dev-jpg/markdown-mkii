using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace MarkdownMkII.RuntimeChecks;

/// <summary>
/// Invoke in the running WinUI process from Visual Studio's Immediate window.
/// This checks the deployed PRI and native resource runtime, which portable tests cannot load.
/// </summary>
public static class LocalizationChecks
{
    public static string Run(string repositoryRoot)
    {
        var app = AppDomain.CurrentDomain.GetAssemblies()
            .Single(assembly => assembly.GetName().Name == "MarkdownMkII.App");
        var strings = app.GetType("MarkdownMkII.Services.Localization.Strings", throwOnError: true)!;
        // Bypass the string cache so every key exercises the real native lookup.
        var resolve = strings.GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<string, string, string?>>();
        var localization = app.GetType("MarkdownMkII.Services.Localization.LocalizationService", throwOnError: true)!;
        var culture = (CultureInfo)localization.GetProperty("Culture")!.GetValue(null)!;
        var language = culture.TwoLetterISOLanguageName == "en" ? "en-US" : "it";
        var catalog = XDocument.Load(Path.Combine(repositoryRoot,
            "src", "MarkdownMkII.App", "Strings", language, "Resources.resw"));
        var entries = catalog.Root!.Elements("data").ToArray();
        if (entries.Length == 0)
            throw new InvalidOperationException("The resource catalog is empty.");

        var failures = new List<string>();
        var threadId = Environment.CurrentManagedThreadId;
        var exceptionCount = 0;
        string? currentKey = null;
        void OnException(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (Environment.CurrentManagedThreadId != threadId) return;
            exceptionCount++;
            failures.Add($"{currentKey}: {args.Exception.GetType().Name} 0x{args.Exception.HResult:X8}");
        }

        AppDomain.CurrentDomain.FirstChanceException += OnException;
        try
        {
            foreach (var entry in entries)
            {
                currentKey = (string)entry.Attribute("name")!;
                if (resolve("Resources", currentKey) != entry.Element("value")!.Value)
                    failures.Add($"{currentKey}: translation differs from the {language} catalog.");
            }

            currentKey = "__RuntimeCheck_MissingKey__";
            if (resolve("Resources", currentKey) is not null)
                failures.Add("A missing key must return null.");
            if (resolve("__RuntimeCheck_MissingMap__", currentKey) is not null)
                failures.Add("A missing map must return null.");
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= OnException;
        }

        if (failures.Count != 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, failures));

        uint length = 1024;
        var package = new StringBuilder((int)length);
        var packageCode = GetCurrentPackageFullName(ref length, package);
        var identity = packageCode == 0 ? package.ToString() : $"unpackaged ({packageCode})";
        return $"PASS: {entries.Length} {language} translations, missing key/map, " +
            $"{exceptionCount} first-chance exceptions. Package: {identity}";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint length, StringBuilder name);
}

using System.Globalization;
using Microsoft.Windows.Globalization;

namespace MarkdownMkII.Services.Localization;

public static class LocalizationService
{
    private const string Stage = "Localization";
    private const string FallbackCultureName = "it-IT";

    public static string StartupLanguage { get; private set; } = string.Empty;

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(FallbackCultureName);

    private static IReadOnlyList<(string Tag, string DisplayName)>? supportedLanguages;

    public static IReadOnlyList<(string Tag, string DisplayName)> SupportedLanguages =>
        supportedLanguages ??= SupportedTags.Select(t => (t.Tag, Strings.Get(Strings.DefaultMap, t.Resource))).ToArray();

    /// <summary>
    /// The interface language to apply for a stored preference. "Match system" is resolved here from the
    /// Windows language list: clearing the persisted override does not reliably drop a language chosen
    /// in an earlier session, so an explicit supported tag is always applied.
    /// </summary>
    public static string EffectiveLanguage(string preference, IEnumerable<string> systemLanguages)
    {
        if (preference.Length != 0) return preference;
        foreach (var language in systemLanguages)
        {
            var primary = language.Split('-')[0];
            foreach (var (tag, _) in SupportedTags)
                if (tag.Length != 0 && string.Equals(tag.Split('-')[0], primary, StringComparison.OrdinalIgnoreCase))
                    return tag;
        }
        return DefaultLanguage;
    }

    private const string DefaultLanguage = "it";
    private static readonly (string Tag, string Resource)[] SupportedTags =
        [(string.Empty, "LanguageSystemDefault"), ("it", "LanguageItalian"), ("en-US", "LanguageEnglish")];

    public static bool RestartRequired => !string.Equals(
        SettingsService.Instance.Appearance.Language,
        StartupLanguage,
        StringComparison.OrdinalIgnoreCase);

    public static void Initialize()
    {
        StartupLanguage = SettingsService.Instance.Appearance.Language;
        Culture = ResolveCulture();
    }

    private static CultureInfo ResolveCulture()
    {
        try
        {
            var languages = ApplicationLanguages.Languages;
            if (languages.Count > 0 && !string.IsNullOrWhiteSpace(languages[0]))
            {
                return CultureInfo.GetCultureInfo(languages[0]);
            }
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, "Lingua attiva non determinabile", ex);
        }

        return CultureInfo.GetCultureInfo(FallbackCultureName);
    }
}

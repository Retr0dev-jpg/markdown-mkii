using Microsoft.Windows.ApplicationModel.Resources;

namespace MarkdownMkII.Services.Localization;

public static class Strings
{
    public const string DefaultMap = "Resources";

    private const string Stage = "Localization";

    private static readonly object gate = new();
    private static readonly Dictionary<string, string> cache = new(StringComparer.Ordinal);
    private static readonly HashSet<string> loggedMisses = new(StringComparer.Ordinal);

    private static ResourceManager? manager;
    private static ResourceContext? context;

    public static string T(string key) => Get(DefaultMap, key);

    public static string Get(string map, string key)
    {
        var cacheKey = map + "/" + key;
        lock (gate)
        {
            if (cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        var value = Resolve(map, key);
        if (value is null)
        {
            LogMissOnce(cacheKey);
            value = key;
        }

        lock (gate)
        {
            cache[cacheKey] = value;
        }

        return value;
    }

    public static string Format(string map, string key, params object[] args)
    {
        var template = Get(map, key);
        try
        {
            return string.Format(LocalizationService.Culture, template, args);
        }
        catch (FormatException ex)
        {
            DiagnosticsService.LogError(Stage, $"Segnaposto non validi in '{map}/{key}'", ex);
            return template;
        }
    }

    private static string? Resolve(string map, string key)
    {
        try
        {
            manager ??= new ResourceManager();
            context ??= CreateContext(manager);
            // RESW strings live under their file's subtree in the compiled PRI.
            // Probing the root with GetValue throws even for valid translated keys.
            var subtree = manager.MainResourceMap.TryGetSubtree(map);
            return subtree is null ? null : TryGetValue(subtree, key);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, $"ResourceManager non disponibile per '{map}/{key}'", ex);
            return null;
        }
    }

    private static string? TryGetValue(ResourceMap map, string key)
    {
        var path = ResourceKey.ToPath(key);
        return TryGetCandidate(map, path) ?? (path == key ? null : TryGetCandidate(map, key));
    }

    private static string? TryGetCandidate(ResourceMap map, string path)
    {
        return context is null
            ? map.TryGetValue(path)?.ValueAsString
            : map.TryGetValue(path, context)?.ValueAsString;
    }

    private static ResourceContext? CreateContext(ResourceManager resourceManager)
    {
        try
        {
            var languages = Microsoft.Windows.Globalization.ApplicationLanguages.Languages;
            if (languages.Count == 0)
            {
                return null;
            }

            var resourceContext = resourceManager.CreateResourceContext();
            resourceContext.QualifierValues["Language"] = string.Join(";", languages);
            return resourceContext;
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, "Contesto delle risorse non creato: si usa quello predefinito", ex);
            return null;
        }
    }

    private static void LogMissOnce(string cacheKey)
    {
        lock (gate)
        {
            if (!loggedMisses.Add(cacheKey))
            {
                return;
            }
        }

        DiagnosticsService.LogError(Stage, $"Risorsa '{cacheKey}' non trovata: si usa la chiave come testo");
    }
}

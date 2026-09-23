using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Models;

/// <summary>Repairs persisted settings before any application component reads them.</summary>
public static class AppSettingsNormalizer
{
    public static void Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Shortcuts ??= [];
        settings.RecentCommands = (settings.RecentCommands ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Take(40).ToList();
        settings.Library ??= new();
        settings.Appearance ??= new();
        settings.Editor ??= new();
        settings.Preview ??= new();
        settings.Export ??= new();
        settings.Diagnostics ??= new();

        if (settings.Version < 2)
        {
            settings.Editor.DefaultViewMode = EditorViewMode.Editor;
            settings.Editor.ShowOutline = false;
        }
        settings.Version = Math.Max(2, settings.Version);

        var library = settings.Library;
        library.DefaultTemplate = MarkdownTemplates.NormalizeId(library.DefaultTemplate);

        var appearance = settings.Appearance;
        appearance.Language ??= string.Empty;
        appearance.Theme = Defined(appearance.Theme, AppTheme.System);
        appearance.Backdrop = Defined(appearance.Backdrop, WindowBackdrop.Mica);

        var editor = settings.Editor;
        editor.FontFamily = string.IsNullOrWhiteSpace(editor.FontFamily) ? "Cascadia Code" : editor.FontFamily;
        editor.FontSize = Positive(editor.FontSize, 14, 10, 32);
        editor.LineHeight = Positive(editor.LineHeight, 1.35, 1, 2.5);
        editor.TabSize = Math.Clamp(editor.TabSize, 2, 8);
        editor.HighlightDebounceMs = Math.Clamp(editor.HighlightDebounceMs, 50, 2000);
        editor.DefaultViewMode = Defined(editor.DefaultViewMode, EditorViewMode.Editor);
        editor.EditorPaneStar = Positive(editor.EditorPaneStar, 1, 0.2, 8);
        editor.PreviewPaneStar = Positive(editor.PreviewPaneStar, 1, 0.2, 8);
        editor.OutlinePaneWidth = Positive(editor.OutlinePaneWidth, 220, 140, 480);

        settings.Preview.LivePreviewDelayMs = Math.Clamp(settings.Preview.LivePreviewDelayMs, 50, 2000);
        settings.Preview.Zoom = Positive(settings.Preview.Zoom, 1, 0.7, 2);
        settings.Export.CssTheme = settings.Export.CssTheme == "dark" ? "dark" : "fluent";
        settings.Diagnostics.LogLevel = Defined(settings.Diagnostics.LogLevel, LogLevel.ErrorsOnly);
    }

    private static double Positive(double value, double fallback, double min, double max)
        => double.IsFinite(value) && value > 0 ? Math.Clamp(value, min, max) : fallback;

    private static T Defined<T>(T value, T fallback) where T : struct, Enum
        => Enum.IsDefined(value) ? value : fallback;
}

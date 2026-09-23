namespace MarkdownMkII.Core.Models;

public enum AppTheme
{
    System,
    Light,
    Dark
}

public enum WindowBackdrop
{
    Mica,
    MicaAlt,
    Acrylic,
    Solid
}

public enum EditorViewMode
{
    Split,
    SplitVertical,
    Editor,
    Preview
}

public enum LogLevel
{
    Off,
    ErrorsOnly,
    Verbose
}

[Flags]
public enum SettingsArea
{
    None = 0,
    Library = 1,
    Appearance = 2,
    Editor = 4,
    Preview = 8,
    Export = 16,
    Diagnostics = 32,
    Commands = 64,
    All = Library | Appearance | Editor | Preview | Export | Diagnostics | Commands
}

public sealed class WindowPlacement
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool IsMaximized { get; set; }
}

public sealed class AppSettings
{
    public Dictionary<string, string?> Shortcuts { get; set; } = [];
    public List<string> RecentCommands { get; set; } = [];
    public int Version { get; set; } = 2;

    public bool IsInitialized { get; set; }

    public LibrarySettings Library { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public EditorSettings Editor { get; set; } = new();

    public PreviewSettings Preview { get; set; } = new();

    public ExportSettings Export { get; set; } = new();

    public DiagnosticsSettings Diagnostics { get; set; } = new();
}

public sealed class LibrarySettings
{

    public string DefaultTemplate { get; set; } = "starter";
}

public sealed class AppearanceSettings
{
    public AppTheme Theme { get; set; } = AppTheme.System;

    public WindowBackdrop Backdrop { get; set; } = WindowBackdrop.Mica;

    public bool ShowNavigationLabels { get; set; }

    public bool RememberWindowPlacement { get; set; } = true;

    public WindowPlacement? Window { get; set; }

    public string Language { get; set; } = string.Empty;
}

public sealed class EditorSettings
{
    public string FontFamily { get; set; } = "Cascadia Code";

    public double FontSize { get; set; } = 14;

    public double LineHeight { get; set; } = 1.35;

    public int TabSize { get; set; } = 4;

    public bool WordWrap { get; set; } = true;

    public bool AutoPairBrackets { get; set; } = true;

    public bool ShowLineNumbers { get; set; } = true;

    public bool HighlightCurrentLine { get; set; } = true;

    public int HighlightDebounceMs { get; set; } = 250;

    public EditorViewMode DefaultViewMode { get; set; } = EditorViewMode.Editor;

    public bool SpellCheck { get; set; } = true;

    public double EditorPaneStar { get; set; } = 1;

    public double PreviewPaneStar { get; set; } = 1;

    public double OutlinePaneWidth { get; set; } = 220;

    public bool ShowOutline { get; set; }

    public bool TypewriterMode { get; set; }

    public bool ShowMinimap { get; set; } = true;
}

public sealed class PreviewSettings
{
    public bool LivePreview { get; set; } = true;

    public int LivePreviewDelayMs { get; set; } = 200;

    public bool SyncScroll { get; set; } = true;

    public bool LoadRemoteImages { get; set; }

    public double Zoom { get; set; } = 1.0;
}

public sealed class ExportSettings
{
    public bool EmbedCss { get; set; } = true;

    public string CssTheme { get; set; } = "fluent";

    public bool IncludeFrontMatter { get; set; } = true;

    public bool IncludeToc { get; set; } = true;
}

public sealed class DiagnosticsSettings
{
    public LogLevel LogLevel { get; set; } = LogLevel.ErrorsOnly;

    /// <summary>Whether an installed copy asks GitHub for a newer release at startup.</summary>
    public bool CheckForUpdates { get; set; } = true;
}

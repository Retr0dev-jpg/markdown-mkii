using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;

namespace MarkdownMkII.ViewModels;

public sealed record SettingsChoice(string Value, string Label);

public partial class SettingsViewModel : ObservableObject
{
    private static IReadOnlyList<SettingsChoice> Choices(params string[] values)
        => values.Select(value => new SettingsChoice(value, Strings.T("SettingsOption." + value))).ToList();

    public IReadOnlyList<SettingsChoice> Themes { get; } = Choices("System", "Light", "Dark");
    public IReadOnlyList<SettingsChoice> Backdrops { get; } = Choices("Mica", "MicaAlt", "Acrylic", "Solid");
    public IReadOnlyList<SettingsChoice> ViewModes { get; } = Choices("Split", "SplitVertical", "Editor", "Preview");
    public IReadOnlyList<SettingsChoice> CssThemes { get; } = Choices("fluent", "dark");
    public IReadOnlyList<SettingsChoice> Templates { get; } = MarkdownMkII.Core.Text.MarkdownTemplates.Ids
        .Select(id => new SettingsChoice(id, Strings.T("Template." + id))).ToList();

    public IReadOnlyList<(string Tag, string DisplayName)> Languages => LocalizationService.SupportedLanguages;

    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = SettingsService.Instance.Appearance.Theme.ToString();

    [ObservableProperty]
    public partial string SelectedBackdrop { get; set; } = SettingsService.Instance.Appearance.Backdrop.ToString();

    [ObservableProperty]
    public partial bool RememberWindowPlacement { get; set; } = SettingsService.Instance.Appearance.RememberWindowPlacement;

    [ObservableProperty]
    public partial string Language { get; set; } = SettingsService.Instance.Appearance.Language;

    [ObservableProperty]
    public partial string FontFamily { get; set; } = SettingsService.Instance.Editor.FontFamily;

    [ObservableProperty]
    public partial double FontSize { get; set; } = SettingsService.Instance.Editor.FontSize;

    [ObservableProperty]
    public partial double LineHeight { get; set; } = SettingsService.Instance.Editor.LineHeight;

    [ObservableProperty]
    public partial double TabSize { get; set; } = SettingsService.Instance.Editor.TabSize;

    [ObservableProperty]
    public partial bool WordWrap { get; set; } = SettingsService.Instance.Editor.WordWrap;

    [ObservableProperty]
    public partial bool AutoPairBrackets { get; set; } = SettingsService.Instance.Editor.AutoPairBrackets;

    [ObservableProperty]
    public partial bool LivePreview { get; set; } = SettingsService.Instance.Preview.LivePreview;

    [ObservableProperty]
    public partial bool SyncScroll { get; set; } = SettingsService.Instance.Preview.SyncScroll;

    [ObservableProperty]
    public partial bool LoadRemoteImages { get; set; } = SettingsService.Instance.Preview.LoadRemoteImages;

    [ObservableProperty]
    public partial bool SpellCheck { get; set; } = SettingsService.Instance.Editor.SpellCheck;

    [ObservableProperty]
    public partial bool ShowLineNumbers { get; set; } = SettingsService.Instance.Editor.ShowLineNumbers;

    [ObservableProperty]
    public partial bool HighlightCurrentLine { get; set; } = SettingsService.Instance.Editor.HighlightCurrentLine;

    [ObservableProperty]
    public partial bool ShowOutline { get; set; } = SettingsService.Instance.Editor.ShowOutline;

    [ObservableProperty]
    public partial bool TypewriterMode { get; set; } = SettingsService.Instance.Editor.TypewriterMode;

    [ObservableProperty]
    public partial bool ShowMinimap { get; set; } = SettingsService.Instance.Editor.ShowMinimap;

    [ObservableProperty]
    public partial string DefaultViewMode { get; set; } = SettingsService.Instance.Editor.DefaultViewMode.ToString();

    [ObservableProperty]
    public partial double PreviewZoom { get; set; } = SettingsService.Instance.Preview.Zoom;

    [ObservableProperty]
    public partial string LogLevel { get; set; } = SettingsService.Instance.Diagnostics.LogLevel.ToString();

    [ObservableProperty]
    public partial string CssTheme { get; set; } = SettingsService.Instance.Export.CssTheme;

    [ObservableProperty]
    public partial bool IncludeToc { get; set; } = SettingsService.Instance.Export.IncludeToc;

    [ObservableProperty]
    public partial string DefaultTemplate { get; set; } = SettingsService.Instance.Library.DefaultTemplate;

    private bool syncing;

    public SettingsViewModel()
    {
        SettingsService.Instance.Changed += (_, _) => ReloadFromSettings();
    }

    public bool RestartRequired => LocalizationService.RestartRequired;

    private void ReloadFromSettings()
    {
        var current = SettingsService.Instance;
        syncing = true;
        try
        {
            SelectedTheme = current.Appearance.Theme.ToString();
            SelectedBackdrop = current.Appearance.Backdrop.ToString();
            RememberWindowPlacement = current.Appearance.RememberWindowPlacement;
            Language = current.Appearance.Language;
            FontFamily = current.Editor.FontFamily;
            FontSize = current.Editor.FontSize;
            LineHeight = current.Editor.LineHeight;
            TabSize = current.Editor.TabSize;
            WordWrap = current.Editor.WordWrap;
            AutoPairBrackets = current.Editor.AutoPairBrackets;
            SpellCheck = current.Editor.SpellCheck;
            ShowLineNumbers = current.Editor.ShowLineNumbers;
            HighlightCurrentLine = current.Editor.HighlightCurrentLine;
            ShowOutline = current.Editor.ShowOutline;
            TypewriterMode = current.Editor.TypewriterMode;
            ShowMinimap = current.Editor.ShowMinimap;
            DefaultViewMode = current.Editor.DefaultViewMode.ToString();
            LivePreview = current.Preview.LivePreview;
            SyncScroll = current.Preview.SyncScroll;
            LoadRemoteImages = current.Preview.LoadRemoteImages;
            PreviewZoom = current.Preview.Zoom;
            CssTheme = current.Export.CssTheme;
            IncludeToc = current.Export.IncludeToc;
            DefaultTemplate = current.Library.DefaultTemplate;
            LogLevel = current.Diagnostics.LogLevel.ToString();
            OnPropertyChanged(nameof(RestartRequired));
        }
        finally
        {
            syncing = false;
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (syncing || !Enum.TryParse<AppTheme>(value, out var parsed))
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Appearance, s => s.Appearance.Theme = parsed);
    }

    partial void OnSelectedBackdropChanged(string value)
    {
        if (syncing || !Enum.TryParse<WindowBackdrop>(value, out var parsed))
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Appearance, s => s.Appearance.Backdrop = parsed);
    }

    partial void OnRememberWindowPlacementChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Appearance, s => s.Appearance.RememberWindowPlacement = value);
    }

    partial void OnLanguageChanged(string value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Appearance, s => s.Appearance.Language = value ?? string.Empty);
        OnPropertyChanged(nameof(RestartRequired));
    }

    partial void OnFontFamilyChanged(string value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.FontFamily = value);
    }

    partial void OnFontSizeChanged(double value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.FontSize = value);
    }

    partial void OnLineHeightChanged(double value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(
            SettingsArea.Editor,
            s => s.Editor.LineHeight = Math.Clamp(value, 1.0, 2.5));
    }

    partial void OnTabSizeChanged(double value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.TabSize = (int)Math.Clamp(Math.Round(value), 2, 8));
    }

    partial void OnWordWrapChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.WordWrap = value);
    }

    partial void OnAutoPairBracketsChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.AutoPairBrackets = value);
    }

    partial void OnLivePreviewChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Preview, s => s.Preview.LivePreview = value);
    }

    partial void OnSyncScrollChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Preview, s => s.Preview.SyncScroll = value);
    }

    partial void OnLoadRemoteImagesChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Preview, s => s.Preview.LoadRemoteImages = value);
    }

    partial void OnSpellCheckChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.SpellCheck = value);
    }

    partial void OnShowLineNumbersChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.ShowLineNumbers = value);
    }

    partial void OnHighlightCurrentLineChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.HighlightCurrentLine = value);
    }

    partial void OnShowOutlineChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.ShowOutline = value);
    }

    partial void OnTypewriterModeChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.TypewriterMode = value);
    }

    partial void OnShowMinimapChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.ShowMinimap = value);
    }

    partial void OnCssThemeChanged(string value)
    {
        if (syncing || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Export, s => s.Export.CssTheme = value);
    }

    partial void OnIncludeTocChanged(bool value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Export, s => s.Export.IncludeToc = value);
    }

    partial void OnDefaultTemplateChanged(string value)
    {
        if (syncing || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Library, s => s.Library.DefaultTemplate = value);
    }

    partial void OnDefaultViewModeChanged(string value)
    {
        if (syncing || !Enum.TryParse<EditorViewMode>(value, out var parsed))
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.DefaultViewMode = parsed);
    }

    partial void OnPreviewZoomChanged(double value)
    {
        if (syncing)
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Preview, s => s.Preview.Zoom = value);
    }

    partial void OnLogLevelChanged(string value)
    {
        if (syncing || !Enum.TryParse<LogLevel>(value, out var parsed))
        {
            return;
        }

        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Diagnostics, s => s.Diagnostics.LogLevel = parsed);
    }

    [RelayCommand]
    private Task ResetAsync() => SettingsService.Instance.ResetAsync();

    [RelayCommand]
    private async Task OpenLogAsync()
    {
        try
        {
            await LocalFileLauncher.EnsureTextFileAndOpenAsync(DiagnosticsService.LogPath);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Settings", "Apertura log fallita", ex);
        }
    }
}

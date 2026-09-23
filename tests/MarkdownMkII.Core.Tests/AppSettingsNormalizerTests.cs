using System.Text.Json;
using MarkdownMkII.Core.Models;

namespace MarkdownMkII.Core.Tests;

public class AppSettingsNormalizerTests
{
    [Fact]
    public void RepairsExplicitNullSectionsBeforeMigratingOldSettings()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""
            {"Version":1,"Library":null,"Appearance":null,"Editor":null,"Preview":null,"Export":null,"Diagnostics":null}
            """)!;
        AppSettingsNormalizer.Normalize(settings);
        Assert.Equal(2, settings.Version);
        Assert.Equal(EditorViewMode.Editor, settings.Editor.DefaultViewMode);
        Assert.False(settings.Editor.ShowOutline);
        Assert.NotNull(settings.Shortcuts);
        Assert.NotNull(settings.Preview);
        Assert.NotNull(settings.Export);
        Assert.NotNull(settings.Diagnostics);
    }

    [Fact]
    public void IgnoresLegacyFileStateAndRepairsCommandPreferences()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""
            {"Shortcuts":null,"RecentCommands":[null,"","bold","bold"],"Library":{"Folders":[null,"","vault","VAULT"],"StarredPaths":null,
              "RecentFiles":[null,{"Path":null},{"Path":"a.md","OpenedAt":"2026-01-01T00:00:00Z"},
              {"Path":"A.md","OpenedAt":"2026-02-01T00:00:00Z"}]},
             "Editor":{"PinnedPaths":null,"OpenPaths":null,"CaretByPath":{"a.md":-7,"A.md":12}}}
            """)!;
        AppSettingsNormalizer.Normalize(settings);
        Assert.Empty(settings.Shortcuts);
        Assert.Equal("bold", Assert.Single(settings.RecentCommands));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void RepairsNonFiniteAndInvalidLayoutValues(double value)
    {
        var settings = new AppSettings();
        settings.Editor.FontSize = settings.Editor.LineHeight = settings.Editor.EditorPaneStar = value;
        settings.Preview.Zoom = value;
        settings.Appearance.Theme = (AppTheme)999;
        AppSettingsNormalizer.Normalize(settings);
        Assert.Equal(14, settings.Editor.FontSize);
        Assert.Equal(1.35, settings.Editor.LineHeight);
        Assert.Equal(1, settings.Editor.EditorPaneStar);
        Assert.Equal(1, settings.Preview.Zoom);
        Assert.Equal(AppTheme.System, settings.Appearance.Theme);
    }

    [Fact]
    public void NormalizationIsIdempotentAndKeepsValidPreferences()
    {
        var settings = new AppSettings { Version = 3 };
        settings.Editor.FontSize = 22;
        settings.Editor.ShowOutline = true;
        AppSettingsNormalizer.Normalize(settings);
        var once = JsonSerializer.Serialize(settings);
        AppSettingsNormalizer.Normalize(settings);
        Assert.Equal(once, JsonSerializer.Serialize(settings));
        Assert.Equal(3, settings.Version);
        Assert.Equal(22, settings.Editor.FontSize);
        Assert.True(settings.Editor.ShowOutline);
    }
}

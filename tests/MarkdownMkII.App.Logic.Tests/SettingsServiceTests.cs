using System.Text.Json;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Services;

namespace MarkdownMkII.App.Logic.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "mkii-settings-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(directory, "settings.json");

    [Fact]
    public async Task AwaitingConcurrentUpdatesPersistsEveryMutation()
    {
        var service = new SettingsService(FilePath);
        await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(index => service.UpdateStateAsync(settings => settings.RecentCommands.Add($"command{index}"))));
        await service.FlushAsync();
        var persisted = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(FilePath), SettingsService.JsonOptions)!;
        Assert.Equal(40, persisted.RecentCommands.Count);
        Assert.Equal(service.Current.RecentCommands, persisted.RecentCommands);
    }

    [Fact]
    public async Task ConcurrentShortcutAssignmentsRemainUnique()
    {
        var service = new SettingsService(FilePath);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => service.UpdateStateAsync(s => s.Shortcuts["bold"] = "Ctrl+Shift+B")));
        Assert.Equal("Ctrl+Shift+B", Assert.Single(service.Current.Shortcuts).Value);
        var persisted = new SettingsService(FilePath);
        Assert.Single(persisted.Current.Shortcuts);
    }

    [Fact]
    public void NullJsonSectionsAreRepairedBeforeLegacyMigration()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, """{"Version":1,"Appearance":null,"Editor":null,"Library":null}""");
        var service = new SettingsService(FilePath);
        Assert.Equal(EditorViewMode.Editor, service.Editor.DefaultViewMode);
        Assert.NotNull(service.Current.Shortcuts);
        Assert.Equal(2, service.Current.Version);
    }

    [Fact]
    public async Task ResetPreservesLanguageAndRestoresPreferencesOnDisk()
    {
        var service = new SettingsService(FilePath);
        await service.UpdateStateAsync(settings =>
        {
            settings.Shortcuts["bold"] = "Ctrl+Shift+B";
            settings.Appearance.Language = "it";
            settings.Editor.FontSize = 27;
        });
        await service.ResetAsync();
        var persisted = new SettingsService(FilePath);
        Assert.Empty(persisted.Current.Shortcuts);
        Assert.Equal("it", persisted.Appearance.Language);
        Assert.Equal(14, persisted.Editor.FontSize);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}

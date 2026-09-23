using MarkdownMkII.Core.Editor;

namespace MarkdownMkII.Core.Tests;

public sealed class CommandRegistryTests
{
    [Theory]
    [InlineData("Ctrl+Shift+P", true)]
    [InlineData("Ctrl+Alt+E", false)]
    [InlineData("Ctrl+C", false)]
    [InlineData("Ctrl+Tab", false)]
    [InlineData("Alt+F4", false)]
    [InlineData("Ctrl+Left", false)]
    [InlineData("Shift+A", false)]
    [InlineData("F5", true)]
    public void GesturesRespectTextEditingAndSystemKeys(string text, bool allowed)
    {
        Assert.True(CommandGesture.TryParse(text, out var gesture)); Assert.Equal(allowed, gesture.CanAssign);
        Assert.True(CommandGesture.TryParse(gesture.ToString(), out var roundtrip)); Assert.Equal(gesture, roundtrip);
    }

    [Fact]
    public void SearchUsesAccentsSynonymsAndAvailabilityAndDeduplicatesHome()
    {
        CommandSearchEntry[] commands = [new("bold", "Grassetto", "Formato", "forte bold", true), new("open", "Apri nota", "Note", "cerca", true), new("hidden", "Privato", "Note", "", false)];
        Assert.Equal(new[] { "bold" }, CommandSearch.Find(commands, "fòrte", [], []));
        Assert.Equal(new[] { "open", "bold" }, CommandSearch.Find(commands, "", ["open", "hidden"], ["open", "bold"]));
    }
}

using MarkdownMkII.Editor;

namespace MarkdownMkII.App.Logic.Tests;

public sealed class EditorContextTests
{
    [Theory]
    [InlineData("| A | B |\n|---|---|\n| x | y |", "x", true, false, false, false)]
    [InlineData("```md\n| A | B |\n|---|---|\n```", "A", false, false, false, true)]
    [InlineData("```md\n# Example\n- item\n```", "Example", false, false, false, true)]
    [InlineData("- first\n- second", "second", false, true, false, false)]
    [InlineData("## Heading\n\nBody", "Heading", false, false, true, false)]
    [InlineData("Heading\n=======\n\nBody", "Heading", false, false, true, false)]
    [InlineData("ordinary | prose", "prose", false, false, false, false)]
    public void UsesMarkdownStructureRatherThanPunctuation(string text, string selected, bool table, bool list, bool heading, bool code)
    {
        var context = EditorContext.Inspect(text, text.IndexOf(selected, StringComparison.Ordinal), selected.Length);
        Assert.Equal(new EditorContext(true, table, list, heading, code), context);
    }

    [Fact]
    public void SelectionAcrossHeadingAndBodyIsNotAHeadingContext()
        => Assert.False(EditorContext.Inspect("# Title\n\nBody", 2, 11).IsHeading);

    [Fact]
    public void EmptyDocumentHasNoContextualOperations()
        => Assert.Equal(default, EditorContext.Inspect(string.Empty, 10, 4));
}

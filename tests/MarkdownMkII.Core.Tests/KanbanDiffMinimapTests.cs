using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class KanbanDiffMinimapTests
{
    [Fact]
    public void Kanban_Extracts_And_Moves_Between_Headings()
    {
        var text = """
            # Todo

            - [ ] Write docs

            # Doing

            - [ ] Code

            # Done

            - [x] Ship
            """;
        var columns = MarkdownKanban.Extract(text);
        Assert.Equal(3, columns.Count);
        Assert.Contains(columns, column => column.Name == "Todo" && column.Cards.Count == 1);
        var write = columns.SelectMany(column => column.Cards).Single(card => card.Title.Contains("Write", StringComparison.Ordinal));
        var moved = MarkdownKanban.Move(text, write.Line, 1);
        Assert.Contains("Doing", moved.Text);
        var doing = MarkdownKanban.Extract(moved.Text).Single(column => column.Name == "Doing");
        Assert.Contains(doing.Cards, card => card.Title.Contains("Write", StringComparison.Ordinal));
        var toggled = MarkdownKanban.Toggle(text, write.Line);
        Assert.Contains("- [x] Write docs", toggled.Text);
        var yaml = """
            ---
            kanban: Inbox, Doing, Done
            ---

            - [ ] Draft
            - [x] Shipped
            """;
        var declared = MarkdownKanban.Extract(yaml);
        Assert.Equal(["Inbox", "Doing", "Done"], declared.Select(column => column.Name).ToList());
        Assert.Contains(declared.Single(column => column.Name == "Inbox").Cards, card => card.Title == "Draft");
        Assert.Contains(declared.Single(column => column.Name == "Done").Cards, card => card.Title == "Shipped");
    }

    [Fact]
    public void Diff_Summarizes_Line_Changes()
    {
        var summary = TextDiff.Summarize("alpha\nkeep\n", "beta\nkeep\ngamma\n");
        Assert.Contains("+", summary);
        Assert.Contains("−", summary);
        Assert.Contains("- alpha", summary);
        Assert.Contains("+ beta", summary);
        Assert.Contains("+ gamma", summary);
    }

    [Fact]
    public void Minimap_Plans_Density_Marks()
    {
        var marks = TextMinimap.Plan("short\n" + new string('x', 80) + "\n\nend\n", 100, currentLine: 1);
        Assert.NotEmpty(marks);
        Assert.Contains(marks, mark => mark.Current);
        Assert.True(marks.Max(mark => mark.Density) > marks.Min(mark => mark.Density));
    }
}

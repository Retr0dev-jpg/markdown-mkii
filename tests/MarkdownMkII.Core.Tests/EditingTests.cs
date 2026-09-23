using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class EditingTests
{
    [Fact]
    public void ToggleBold_Wraps_And_Unwraps()
    {
        var wrap = MarkdownEditing.ToggleBold("hello", 0, 5);
        Assert.Equal("**hello**", wrap.Text);
        var unwrap = MarkdownEditing.ToggleBold(wrap.Text, wrap.SelectionStart, wrap.SelectionLength);
        Assert.Equal("hello", unwrap.Text);
    }

    [Fact]
    public void ToggleHeading_Sets_Level()
    {
        var result = MarkdownEditing.ToggleHeading("Title", 0, 5, 2);
        Assert.Equal("## Title", result.Text);
        var cleared = MarkdownEditing.ToggleHeading(result.Text, 0, result.Text.Length, 0);
        Assert.Equal("Title", cleared.Text);
    }

    [Fact]
    public void CycleHeading_Walks_Levels_Then_Clears()
    {
        var current = "Title";
        for (var level = 1; level <= 6; level++)
        {
            var result = MarkdownEditing.CycleHeading(current, 0, current.Length);
            Assert.StartsWith(new string('#', level) + " ", result.Text);
            current = result.Text;
        }

        var cleared = MarkdownEditing.CycleHeading(current, 0, current.Length);
        Assert.Equal("Title", cleared.Text);
        Assert.Equal(0, MarkdownEditing.HeadingLevel(cleared.Text));
        Assert.Equal(2, MarkdownEditing.HeadingLevel("## Title"));
    }

    [Fact]
    public void DeleteLines_And_LineSelection()
    {
        var deleted = MarkdownEditing.DeleteLines("aaa\nbbb\nccc", 4, 0);
        Assert.Equal("aaa\nccc", deleted.Text);
        var range = MarkdownEditing.LineSelection("aaa\nbbb\nccc", 4, 0);
        Assert.Equal(4, range.Start);
        Assert.Equal(4, range.Length);
    }

    [Fact]
    public void ToggleTaskAtCaret_Flips_Marker()
    {
        var checkedTask = MarkdownEditing.ToggleTaskAtCaret("- [ ] one", 0);
        Assert.NotNull(checkedTask);
        Assert.Equal("- [x] one", checkedTask.Value.Text);
        var uncheckedTask = MarkdownEditing.ToggleTaskAtCaret(checkedTask.Value.Text, 0);
        Assert.Equal("- [ ] one", uncheckedTask!.Value.Text);
        Assert.Null(MarkdownEditing.ToggleTaskAtCaret("plain", 0));
    }

    [Fact]
    public void TitleCase_And_Math()
    {
        var title = MarkdownEditing.TitleCase("hello world", 0, 11);
        Assert.Equal("Hello World", title.Text);
        var math = MarkdownEditing.InsertMath("alpha", 0, 5);
        Assert.Equal("$alpha$", math.Text);
        var block = MarkdownEditing.InsertMathBlock("x", 0, 1);
        Assert.Contains("$$", block.Text);
        Assert.Contains("x", block.Text);
    }

    [Fact]
    public void Highlight_Superscript_And_Definition()
    {
        Assert.Equal("==hi==", MarkdownEditing.ToggleHighlight("hi", 0, 2).Text);
        Assert.Equal("^2^", MarkdownEditing.ToggleSuperscript("2", 0, 1).Text);
        Assert.Equal("~i~", MarkdownEditing.ToggleSubscript("i", 0, 1).Text);
        Assert.Equal("++x++", MarkdownEditing.ToggleInserted("x", 0, 1).Text);
        var definition = MarkdownEditing.InsertDefinitionList("Term", 0, 4);
        Assert.Contains("Term", definition.Text);
        Assert.Contains(": Definition", definition.Text);
        Assert.Equal("hello-world", MarkdownEditing.HeadingAnchor("Hello World!"));
    }

    [Fact]
    public void AdvanceTableCell_Moves_And_Inserts_Row()
    {
        var table = "| a | b |\n| --- | --- |\n| 1 | 2 |\n";
        var start = table.IndexOf('1', StringComparison.Ordinal);
        var next = MarkdownEditing.AdvanceTableCell(table, start, 0, reverse: false);
        Assert.NotNull(next);
        Assert.Equal("2", next.Value.Text.Substring(next.Value.SelectionStart, next.Value.SelectionLength).Trim());
        var last = MarkdownEditing.AdvanceTableCell(next.Value.Text, next.Value.SelectionStart, 0, reverse: false);
        Assert.NotNull(last);
        Assert.Contains("|  |  |", last.Value.Text);
        Assert.Null(MarkdownEditing.AdvanceTableCell("plain", 0, 0, reverse: false));
    }

    [Fact]
    public void Table_Structure_And_Indent_And_Renumber()
    {
        var table = "| a | b |\n| --- | --- |\n| 1 | 2 |\n";
        var start = table.IndexOf('1', StringComparison.Ordinal);
        var added = MarkdownEditing.AddTableRow(table, start, 0);
        Assert.NotNull(added);
        Assert.True(added.Value.Text.Split('\n').Count(line => line.StartsWith('|')) >= 4);
        var aligned = MarkdownEditing.AlignTableColumn(table, start, 0, "center");
        Assert.NotNull(aligned);
        Assert.Contains(":---:", aligned.Value.Text);
        var indented = MarkdownEditing.Indent("- item", 0, 0, 4);
        Assert.StartsWith("    - item", indented.Text);
        var numbered = MarkdownEditing.FormatDocument("1. a\n1. b\n");
        Assert.Contains("1. a", numbered.Text);
        Assert.Contains("2. b", numbered.Text);
    }

    [Fact]
    public void ToggleList_And_Task()
    {
        var list = MarkdownEditing.ToggleList("one\ntwo", 0, 7, ordered: false);
        Assert.Contains("- one", list.Text);
        var task = MarkdownEditing.ToggleTaskItem("one", 0, 3);
        Assert.StartsWith("- [ ]", task.Text);
        var checkedTask = MarkdownEditing.SetTaskChecked("- [ ] one", 0, true);
        Assert.Contains("[x]", checkedTask.Text);
        var inlinePreserved = MarkdownEditing.SetTaskChecked("- [ ] usa `[x]` qui", 0, true);
        Assert.Equal("- [x] usa `[x]` qui", inlinePreserved.Text);
        var unindent = MarkdownEditing.Unindent("    item", 0, 4, 4);
        Assert.Equal("item", unindent.Text);
    }

    [Fact]
    public void InsertTable_Has_Header_And_Rows()
    {
        var table = MarkdownEditing.InsertTable(string.Empty, 0, 2, 3);
        Assert.Contains("| Col1 |", table.Text);
        Assert.Contains("---", table.Text);
        Assert.DoesNotContain('\r', table.Text);
    }

    [Fact]
    public void InsertLink_And_Image()
    {
        var link = MarkdownEditing.InsertLink("hello", 0, 5, "https://example.com");
        Assert.Equal("[hello](https://example.com)", link.Text);
        var image = MarkdownEditing.InsertImage(string.Empty, 0, 0, "./pic.png", "alt");
        Assert.Equal("![alt](./pic.png)", image.Text);
        var inserted = MarkdownEditing.InsertText("ab", 1, 0, "X");
        Assert.Equal("aXb", inserted.Text);
        Assert.True(MarkdownEditing.LooksLikeWebUrl("https://example.com/x"));
        Assert.False(MarkdownEditing.LooksLikeWebUrl("notaurl"));
        var emptyLink = MarkdownEditing.InsertLink(string.Empty, 0, 0, "https://example.com");
        Assert.Contains("example.com", emptyLink.Text);
    }

    [Fact]
    public void InsertTableOfContents_Uses_Heading_Ids()
    {
        var toc = MarkdownEditing.InsertTableOfContents("# Hello\n\n## World\n", 0, "TOC");
        Assert.Contains("## TOC", toc.Text);
        Assert.Contains("](#hello)", toc.Text);
        Assert.Contains("](#world)", toc.Text);
    }

    [Fact]
    public void MoveLines_Swaps_With_Neighbor()
    {
        var up = MarkdownEditing.MoveLines("aaa\nbbb\nccc", 4, 0, -1);
        Assert.Equal("bbb\naaa\nccc", up.Text);
        var down = MarkdownEditing.MoveLines("aaa\nbbb\nccc", 4, 0, 1);
        Assert.Equal("aaa\nccc\nbbb", down.Text);
        var blocked = MarkdownEditing.MoveLines("aaa\nbbb", 0, 0, -1);
        Assert.Equal("aaa\nbbb", blocked.Text);
    }

    [Fact]
    public void MoveHeadingSection_Swaps_Siblings()
    {
        var text = "# A\n\n## x\n\n# B\n\nbody";
        var down = MarkdownEditing.MoveHeadingSection(text, 0, 1);
        Assert.NotNull(down);
        Assert.StartsWith("# B", down.Value.Text);
        Assert.Contains("# A", down.Value.Text);
        Assert.True(down.Value.Text.IndexOf("# B", StringComparison.Ordinal) < down.Value.Text.IndexOf("# A", StringComparison.Ordinal));
        Assert.Contains("## x", down.Value.Text[(down.Value.Text.IndexOf("# A", StringComparison.Ordinal))..]);

        var up = MarkdownEditing.MoveHeadingSection(down.Value.Text, down.Value.Text.IndexOf("# A", StringComparison.Ordinal), -1);
        Assert.NotNull(up);
        Assert.StartsWith("# A", up.Value.Text);

        var nested = MarkdownEditing.MoveHeadingSection(text, text.IndexOf("## x", StringComparison.Ordinal), 1);
        Assert.Null(nested);

        var selected = MarkdownEditing.SelectHeadingSection(text, 0);
        Assert.NotNull(selected);
        Assert.StartsWith("# A", selected.Value.Text.Substring(selected.Value.SelectionStart, selected.Value.SelectionLength));
        Assert.Contains("## x", selected.Value.Text.Substring(selected.Value.SelectionStart, selected.Value.SelectionLength));
        Assert.DoesNotContain("# B", selected.Value.Text.Substring(selected.Value.SelectionStart, selected.Value.SelectionLength));
    }

    [Fact]
    public void DuplicateLines_Copies_Current_Block()
    {
        var dup = MarkdownEditing.DuplicateLines("aaa\nbbb", 0, 0);
        Assert.Equal("aaa\naaa\nbbb", dup.Text);
        var last = MarkdownEditing.DuplicateLines("aaa\nbbb", 4, 0);
        Assert.Equal("aaa\nbbb\nbbb", last.Text);
        var block = MarkdownEditing.DuplicateLines("aaa\nbbb\nccc", 0, 7);
        Assert.Equal("aaa\nbbb\naaa\nbbb\nccc", block.Text);
    }

    [Fact]
    public void FormatDocument_Trims_And_Caps_Blank_Lines()
    {
        var formatted = MarkdownEditing.FormatDocument("aaa  \n\n\n\nbbb   \n");
        Assert.Equal("aaa\n\n\nbbb\n", formatted.Text);
    }

    [Fact]
    public void ContinueBlockOnEnter_Extends_And_Breaks_Lists()
    {
        var continued = MarkdownEditing.ContinueBlockOnEnter("- item", 6);
        Assert.NotNull(continued);
        Assert.Equal("- item\n- ", continued.Value.Text);
        var numbered = MarkdownEditing.ContinueBlockOnEnter("1. one", 6);
        Assert.Equal("1. one\n2. ", numbered!.Value.Text);
        var broken = MarkdownEditing.ContinueBlockOnEnter("- ", 2);
        Assert.Equal(string.Empty, broken!.Value.Text);
        var ignored = MarkdownEditing.ContinueBlockOnEnter("plain", 5);
        Assert.Null(ignored);
    }

    [Fact]
    public void ToggleHtmlComment_Wraps_And_Unwraps()
    {
        var wrapped = MarkdownEditing.ToggleHtmlComment("hello", 0, 5);
        Assert.Equal("<!-- hello -->", wrapped.Text);
        var unwrapped = MarkdownEditing.ToggleHtmlComment(wrapped.Text, 0, wrapped.Text.Length);
        Assert.Equal("hello", unwrapped.Text);
    }

    [Fact]
    public void Sort_Join_Case_Wiki_Footnote_Callout_FrontMatter()
    {
        var sorted = MarkdownEditing.SortSelectedLines("c\na\nb", 0, 5);
        Assert.Equal("a\nb\nc", sorted.Text);
        var joined = MarkdownEditing.JoinLines("one\ntwo", 0, 7);
        Assert.Equal("one two", joined.Text);
        var upper = MarkdownEditing.ChangeCase("Hi", 0, 2, upper: true);
        Assert.Equal("HI", upper.Text);
        var wiki = MarkdownEditing.InsertWikilink("see notes", 4, 5);
        Assert.Equal("see [[notes]]", wiki.Text);
        var note = MarkdownEditing.InsertFootnote("Hello", 5, 0);
        Assert.Contains("[^1]", note.Text);
        Assert.Contains("[^1]: ", note.Text);
        var callout = MarkdownEditing.InsertCallout("body", 0, 4, "warning");
        Assert.Contains(":::warning", callout.Text);
        Assert.Contains("body", callout.Text);
        var front = MarkdownEditing.InsertFrontMatter("# Title\n");
        Assert.StartsWith("---\ntitle: ", front.Text);
        var skipped = MarkdownEditing.InsertFrontMatter("---\ntitle: x\n---\n");
        Assert.StartsWith("---", skipped.Text);
        Assert.Equal(0, skipped.SelectionStart);
        var mermaid = MarkdownEditing.InsertMermaid("", 0, 0, "sequence");
        Assert.Contains("```mermaid", mermaid.Text);
        Assert.Contains("sequenceDiagram", mermaid.Text);
        var pie = MarkdownEditing.InsertMermaid("", 0, 0, "pie");
        Assert.Contains("pie title", pie.Text);
        var typo = MarkdownEditing.ApplyTypographer("Hello---world... and http://a--b.com\n```\na--b\n```\n");
        Assert.Contains("—", typo.Text);
        Assert.Contains("…", typo.Text);
        Assert.Contains("http://a--b.com", typo.Text);
        Assert.Contains("a--b", typo.Text);
        var quotes = MarkdownEditing.ApplyTypographer("\"Hi\" and it's fine");
        Assert.Contains("“Hi”", quotes.Text);
        Assert.Contains("it’s", quotes.Text);
        var extracted = MarkdownEditing.ExtractToNote("# Hello\n\nBody here\n\n# Other\n", 0, 0, "Note");
        Assert.Contains("[[Note]]", extracted.Text);
        Assert.Contains("# Other", extracted.Text);
        Assert.Contains("# Hello", extracted.NoteBody);
        Assert.Contains("Body here", extracted.NoteBody);
        Assert.DoesNotContain("# Other", extracted.NoteBody);
        Assert.Equal("Note", extracted.Stem);
        var linked = WikiIndex.Linkify("See Alpha and [[Alpha]] and `Alpha`.\n```\nAlpha\n```\n", "Alpha");
        Assert.Contains("[[Alpha]]", linked);
        Assert.DoesNotContain("[[[[Alpha]]]]", linked);
        Assert.Contains("```\nAlpha\n```", linked);
    }

    [Fact]
    public void Shorter_Markers_Do_Not_Unwrap_Longer_Ones()
    {
        Assert.Equal("***bold***", MarkdownEditing.ToggleWrap("**bold**", 2, 4, "*", "*").Text);
        Assert.Equal("**both**", MarkdownEditing.ToggleWrap("***both***", 3, 4, "*", "*").Text);
        Assert.Equal("*both*", MarkdownEditing.ToggleWrap("***both***", 3, 4, "**", "**").Text);
        Assert.Equal("it", MarkdownEditing.ToggleWrap("*it*", 1, 2, "*", "*").Text);
        Assert.Equal("~~~x~~~", MarkdownEditing.ToggleWrap("~~x~~", 2, 1, "~", "~").Text);
        Assert.Equal("bold", MarkdownEditing.ToggleWrap("**bold**", 0, 8, "**", "**").Text);
    }

    [Fact]
    public void MapSelection_Keeps_The_Caret_Outside_The_Change()
    {
        const string body = "---\ntitle: A\n---\nBody text";
        var added = body.Replace("title: A\n", "title: A\nstatus: draft\n");
        var caret = body.IndexOf("text", StringComparison.Ordinal);
        Assert.Equal(caret + "status: draft\n".Length, MarkdownEditing.MapSelection(body, added, caret, 0).SelectionStart);
        Assert.Equal(1, MarkdownEditing.MapSelection("abcdef", "abXYZdef", 1, 1).SelectionStart);
        Assert.Equal(2, MarkdownEditing.MapSelection("abcdef", "abXYZdef", 2, 1).SelectionStart);
    }

    [Fact]
    public void AutoPair_Only_Pairs_Brackets_At_Boundaries()
    {
        Assert.Null(MarkdownEditing.TryAutoPair("l", 1, 0, '\''));
        Assert.Null(MarkdownEditing.TryAutoPair("- ", 2, 0, '*'));
        Assert.Null(MarkdownEditing.TryAutoPair("ab", 1, 0, '('));
        Assert.Equal("f()", MarkdownEditing.TryAutoPair("f", 1, 0, '(')!.Value.Text);
        Assert.Null(MarkdownEditing.TryAutoPair("don", 3, 0, '`'));
        var skip = MarkdownEditing.TryAutoPair("f()", 2, 0, ')')!.Value;
        Assert.Equal("f()", skip.Text);
        Assert.Equal(3, skip.SelectionStart);
    }

    [Fact]
    public void AutoPair_And_Timestamp()
    {
        var wrap = MarkdownEditing.TryAutoPair("hi", 0, 2, '*');
        Assert.Equal("*hi*", wrap!.Value.Text);
        var insert = MarkdownEditing.TryAutoPair("", 0, 0, '[');
        Assert.Equal("[]", insert!.Value.Text);
        Assert.Equal(1, insert.Value.SelectionStart);
        var skip = MarkdownEditing.TryAutoPair("x", 0, 0, 'a');
        Assert.Null(skip);
        Assert.Equal("$x$", MarkdownEditing.TryAutoPair("x", 0, 1, '$')!.Value.Text);
        Assert.Equal("~x~", MarkdownEditing.TryAutoPair("x", 0, 1, '~')!.Value.Text);
        var stamp = MarkdownEditing.InsertTimestamp("x", 1, 0, new DateTimeOffset(2026, 9, 10, 18, 0, 0, TimeSpan.Zero));
        Assert.Contains("2026-09-10 18:00", stamp.Text);
    }
}

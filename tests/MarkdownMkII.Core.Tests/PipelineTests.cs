using MarkdownMkII.Core.Preview;

namespace MarkdownMkII.Core.Tests;

public class PipelineTests
{
    [Fact]
    public void Alphabetic_And_Roman_Lists_Keep_Their_Markers()
    {
        var alpha = Assert.IsType<ListBlockIr>(Assert.Single(PreviewBuilder.Build("b. beta\nc. gamma\n").Blocks));
        Assert.Equal("b.", alpha.Marker(alpha.StartNumber));
        var roman = Assert.IsType<ListBlockIr>(Assert.Single(PreviewBuilder.Build("iv) four\nv) five\n").Blocks));
        Assert.Equal("iv)", roman.Marker(roman.StartNumber));
        Assert.Equal("v)", roman.Marker(roman.StartNumber + 1));
    }

    [Fact]
    public void Shared_Definition_Keeps_Every_Term()
    {
        var list = Assert.IsType<DefinitionListIr>(Assert.Single(PreviewBuilder.Build("Apple\nBanana\n:   A fruit\n").Blocks));
        var item = Assert.Single(list.Items);
        Assert.Contains(item.Term, inline => inline is LineBreakInline);
        Assert.Contains(item.Term, inline => inline is TextInline { Text: "Apple" });
        Assert.Single(item.Definitions);
    }

    [Fact]
    public void Task_Checkboxes_Work_In_Quotes_And_Belong_To_Their_Own_Item()
    {
        Assert.Equal("> - [x] quoted", MarkdownMkII.Core.Text.MarkdownEditing.SetTaskChecked("> - [ ] quoted", 0, true).Text);
        var list = Assert.IsType<ListBlockIr>(Assert.Single(PreviewBuilder.Build("- - [ ] nested\n").Blocks));
        Assert.Null(Assert.Single(list.Items).Checked);
    }

    [Fact]
    public void Parses_GitHubFlavoredMarkdown()
    {
        var md = """
            ---
            title: Demo
            ---
            # Hello

            Paragraph with **bold**, *italic*, `code`, [link](https://example.com) and ~~strike~~.

            - [x] done
            - [ ] todo

            1. one
            2. two

            | A | B |
            |---|---|
            | 1 | 2 |

            > quote

            ```csharp
            var x = 1;
            ```

            Footnote[^1]

            [^1]: note
            """;

        var parsed = DocumentParser.Parse(md);
        Assert.Equal("Demo", parsed.FrontMatter["title"]);
        Assert.Contains(parsed.Preview.Blocks, b => b is HeadingBlockIr { Level: 1 });
        Assert.Contains(parsed.Preview.Blocks, b => b is ListBlockIr { Ordered: false });
        Assert.Contains(parsed.Preview.Blocks, b => b is ListBlockIr { Ordered: true });
        Assert.Contains(parsed.Preview.Blocks, b => b is TableBlockIr);
        Assert.Contains(parsed.Preview.Blocks, b => b is QuoteBlockIr);
        Assert.Contains(parsed.Preview.Blocks, b => b is CodeBlockIr { Language: "csharp" });
        Assert.Contains(parsed.Preview.Blocks, b => b is FrontMatterIr);
        Assert.True(parsed.Outline.Count >= 1);
        Assert.True(parsed.Stats.Words > 5);
    }

    [Fact]
    public void Preview_Renders_Footnotes()
    {
        var parsed = DocumentParser.Parse("Hello[^1]\n\n[^1]: a note");
        var quote = parsed.Preview.Blocks.OfType<QuoteBlockIr>().LastOrDefault();
        Assert.NotNull(quote);
        Assert.Contains(quote.Children, child => child is FootnoteBlockIr { Label: not null });
    }

    [Fact]
    public void Preview_DoesNotRenderRawHtml()
    {
        var parsed = DocumentParser.Parse("<script>alert(1)</script>\n\nHello");
        Assert.DoesNotContain(parsed.Preview.Blocks, b => b is HtmlStubIr);
        Assert.Contains(parsed.Preview.Blocks, b => b is ParagraphBlockIr);
    }

    [Fact]
    public void TaskList_IsChecked()
    {
        var parsed = DocumentParser.Parse("- [x] done\n- [ ] todo");
        var list = Assert.IsType<ListBlockIr>(parsed.Preview.Blocks.Single(b => b is ListBlockIr));
        Assert.True(list.Items[0].Checked);
        Assert.False(list.Items[1].Checked);
    }

    [Fact]
    public void Images_ResolveRelativeToDocument()
    {
        var parsed = DocumentParser.Parse("![alt](./pic.png)", "/tmp/notes/doc.md");
        var paragraph = Assert.IsType<ParagraphBlockIr>(parsed.Preview.Blocks[0]);
        var image = Assert.IsType<LinkInline>(paragraph.Inlines[0]);
        Assert.True(image.IsImage);
        Assert.Contains("pic.png", image.Url.Replace('\\', '/'));
    }

    [Fact]
    public void Wikilinks_Become_Markdown_Links()
    {
        var parsed = DocumentParser.Parse("See [[notes]] and [[other|Label]].", "/tmp/vault/doc.md");
        var paragraph = Assert.IsType<ParagraphBlockIr>(parsed.Preview.Blocks[0]);
        Assert.Contains(paragraph.Inlines, inline => inline is LinkInline link && link.Url.Replace('\\', '/').Contains("notes.md"));
        Assert.Contains(paragraph.Inlines, inline => inline is LinkInline { Children: [TextInline { Text: "Label" }] });
    }

    [Fact]
    public void Wikilinks_Preserve_Heading_And_Embed()
    {
        var parsed = DocumentParser.Parse("See [[notes#Hello]] and ![[notes]].", "/tmp/vault/doc.md");
        var paragraph = Assert.IsType<ParagraphBlockIr>(parsed.Preview.Blocks[0]);
        Assert.Contains(paragraph.Inlines, inline => inline is LinkInline link && link.Url.Contains("#Hello", StringComparison.Ordinal));
        Assert.Contains(paragraph.Inlines, inline => inline is WikiEmbedInline { Target: "notes", Missing: true });
    }

    [Fact]
    public void Preview_Renders_Callout_Containers()
    {
        var parsed = DocumentParser.Parse(":::info\nHello\n:::\n");
        Assert.Contains(parsed.Preview.Blocks, b => b is CustomContainerIr { Info: "info" });
    }

    [Fact]
    public void Table_Keeps_Column_Alignment()
    {
        var parsed = DocumentParser.Parse("| a | b |\n|:--|--:|\n| 1 | 2 |\n");
        var table = Assert.IsType<TableBlockIr>(parsed.Preview.Blocks.Single(b => b is TableBlockIr));
        Assert.Equal(TableAlignment.Left, table.Grid.Alignments[0]);
        Assert.Equal(TableAlignment.Right, table.Grid.Alignments[1]);
    }

    [Fact]
    public void Preview_Renders_Marked_And_Superscript()
    {
        var parsed = DocumentParser.Parse("==mark== and 2^nd^ and H~2~O and ++ins++");
        var paragraph = Assert.IsType<ParagraphBlockIr>(parsed.Preview.Blocks[0]);
        Assert.Contains(paragraph.Inlines, inline => inline is MarkedInline);
        Assert.Contains(paragraph.Inlines, inline => inline is SuperscriptInline);
        Assert.Contains(paragraph.Inlines, inline => inline is SubscriptInline);
        Assert.Contains(paragraph.Inlines, inline => inline is InsertedInline);
    }

    [Fact]
    public void Parse_Attaches_Diagnostics()
    {
        var parsed = DocumentParser.Parse("```\ncode");
        Assert.NotEmpty(parsed.Diagnostics);
    }
}

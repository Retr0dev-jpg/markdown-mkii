using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class ScrollMapperTests
{
    [Fact]
    public void Maps_Editor_Line_To_Nearest_Block()
    {
        var parsed = DocumentParser.Parse("# A\n\npara\n\n## B\n\nmore\n");
        var heading = ScrollMapper.MapEditorLineToSourceLine(parsed.Preview.Blocks, 0);
        var later = ScrollMapper.MapEditorLineToSourceLine(parsed.Preview.Blocks, 4);
        Assert.True(later >= heading);
        Assert.NotEmpty(ScrollMapper.Flatten(parsed.Preview.Blocks));
        var pages = ScrollMapper.Paginate(parsed.Preview.Blocks, 2);
        Assert.True(pages.Count >= 1);
    }

    [Fact]
    public void Slides_Split_On_H1_And_H2()
    {
        var parsed = DocumentParser.Parse("""
            ---
            title: Deck
            ---

            # One

            a

            ## Two

            b

            ### Nested

            c
            """);
        var slides = ScrollMapper.Slides(parsed.Preview.Blocks);
        Assert.Equal(2, slides.Count);
        Assert.Contains(slides[0], block => block is FrontMatterIr);
        Assert.Contains(slides[1], block => block is HeadingBlockIr heading && heading.Level == 2);
    }
}

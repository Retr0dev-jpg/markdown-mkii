using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class TocTests
{
    [Fact]
    public void Extracts_Headings_WithIds()
    {
        var outline = TocExtractor.Extract("# Alpha\n\n## Beta gamma\n\n### Skip empty\n");
        Assert.Equal(3, outline.Count);
        Assert.Equal(1, outline[0].Level);
        Assert.Equal("Alpha", outline[0].Title);
        Assert.False(string.IsNullOrWhiteSpace(outline[0].Id));
        Assert.Equal("Beta gamma", outline[1].Title);
        Assert.True(outline[0].SourceEndLine >= outline[1].SourceLine);
        Assert.NotNull(TocExtractor.RangeAt(outline, outline[1].SourceLine));
        var crumb = TocExtractor.Breadcrumb(outline, outline[1].SourceLine);
        Assert.Equal(["Alpha", "Beta gamma"], crumb);

        var folded = new HashSet<int>();
        Assert.True(HeadingFold.HasBody(outline[0]));
        Assert.True(HeadingFold.Toggle(folded, outline, outline[0].SourceLine));
        var visible = HeadingFold.Visible(outline, folded);
        Assert.Single(visible);
        Assert.Equal("Alpha", visible[0].Title);
        HeadingFold.UnfoldAll(folded);
        Assert.Equal(3, HeadingFold.Visible(outline, folded).Count);
        HeadingFold.FoldAll(folded, outline);
        Assert.Single(HeadingFold.Visible(outline, folded));
        HeadingFold.Prune(folded, outline.Skip(1).ToList());
        Assert.DoesNotContain(outline[0].SourceLine, folded);
    }
}

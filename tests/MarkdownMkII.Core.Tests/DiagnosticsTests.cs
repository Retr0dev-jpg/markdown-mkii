using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Detects_Unclosed_Fence_And_Empty_Link()
    {
        var issues = MarkdownDiagnostics.Analyze("```csharp\nvar x = 1;\n\n[]()\n");
        Assert.Contains(issues, i => i.Code == "fence");
        Assert.Contains(issues, i => i.Code == "link");
    }

    [Fact]
    public void Detects_Single_Trailing_Space_But_Not_Hard_Break()
    {
        var trailing = MarkdownDiagnostics.Analyze("hello \n");
        Assert.Contains(trailing, i => i.Code == "whitespace");
        var hardBreak = MarkdownDiagnostics.Analyze("hello  \n");
        Assert.DoesNotContain(hardBreak, i => i.Code == "whitespace");
        var inFence = MarkdownDiagnostics.Analyze("```\ncode \n```\n");
        Assert.DoesNotContain(inFence, i => i.Code == "whitespace");
    }

    [Fact]
    public void Detects_Duplicate_Headings()
    {
        var issues = MarkdownDiagnostics.Analyze("# Hello\n\ntext\n\n# Hello\n");
        Assert.Contains(issues, i => i.Code == "heading");
    }

    [Fact]
    public void Detects_Skipped_Heading_Levels()
    {
        var issues = MarkdownDiagnostics.Analyze("# Hello\n\n### Nested\n");
        Assert.Contains(issues, i => i.Code == "heading-skip");
        var ok = MarkdownDiagnostics.Analyze("# Hello\n\n## Nested\n");
        Assert.DoesNotContain(ok, i => i.Code == "heading-skip");
    }

    [Fact]
    public void Detects_Empty_Image_Alt()
    {
        var issues = MarkdownDiagnostics.Analyze("![ok](a.png)\n![](b.png)\n");
        Assert.Contains(issues, i => i.Code == "alt");
        Assert.DoesNotContain(
            MarkdownDiagnostics.Analyze("![cat](b.png)\n"),
            i => i.Code == "alt");
    }

    [Fact]
    public void HeadingRail_Plans_Marks()
    {
        var outline = TocExtractor.Extract("# One\n\n## Two\n\ntext\n");
        var marks = HeadingRail.Plan(outline, 6, 200, currentLine: 0);
        Assert.True(marks.Count >= 1);
        Assert.Contains(marks, mark => mark.Title == "One" && mark.Current);
    }

    [Fact]
    public void MathText_Replaces_Latex_Tokens()
    {
        Assert.Equal("α + β → ∞", MathText.ToDisplay("\\alpha + \\beta \\rightarrow \\infty"));
        Assert.Equal("(a)/(b)", MathText.ToDisplay("\\frac{a}{b}"));
        Assert.Equal("ℝ", MathText.ToDisplay("\\mathbb{R}"));
        Assert.Equal("√(x)", MathText.ToDisplay("\\sqrt{x}"));
        Assert.Equal("√", MathText.ToDisplay("\\sqrt"));
    }
}

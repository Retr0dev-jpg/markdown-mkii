using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class HtmlToMarkdownTests
{
    [Fact]
    public void Converts_Common_Tags()
    {
        Assert.Equal("**hi**", HtmlToMarkdown.Convert("<b>hi</b>"));
        Assert.Contains("# Hello", HtmlToMarkdown.Convert("<h1>Hello</h1>"));
        Assert.Equal("[x](https://a.example)", HtmlToMarkdown.Convert("<a href=\"https://a.example\">x</a>"));
        Assert.True(HtmlToMarkdown.LooksLikeMarkdown("# Title"));
        Assert.False(HtmlToMarkdown.LooksLikeMarkdown("plain text"));
        Assert.StartsWith("<b>hi</b>", HtmlToMarkdown.ExtractFragment("noise <b>hi</b>"));
        var htmlTable = HtmlToMarkdown.Convert("<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>");
        Assert.Contains("| A | B |", htmlTable);
        Assert.Contains("| 1 | 2 |", htmlTable);
        Assert.True(MarkdownTables.TryFromDelimited("A\tB\n1\t2", out var tsv));
        Assert.Contains("| A | B |", tsv);
        Assert.True(MarkdownTables.TryFromDelimited("A,B\n\"1,2\",3", out var csv));
        Assert.Contains("| 1,2 | 3 |", csv);
        Assert.False(MarkdownTables.TryFromDelimited("| A | B |\n| --- | --- |\n| 1 | 2 |", out _));
        Assert.False(MarkdownTables.TryFromDelimited("plain paragraph", out _));
    }
}

using MarkdownMkII.Core.Highlight;
using MarkdownMkII.Core.Preview;

namespace MarkdownMkII.Core.Tests;

public class HighlightTests
{
    [Fact]
    public void Classifies_Heading_Code_And_Link()
    {
        var spans = MarkdownSpanClassifier.Classify("# Title\n\n`code` and [a](https://x.test)\n");
        Assert.Contains(spans, s => s.Kind == MarkdownSpanKind.Heading);
        Assert.Contains(spans, s => s.Kind == MarkdownSpanKind.Code);
        Assert.Contains(spans, s => s.Kind == MarkdownSpanKind.Link);
    }

    [Fact]
    public void Classifies_Wikilinks()
    {
        var spans = MarkdownSpanClassifier.Classify("See [[notes]] here");
        Assert.Contains(spans, s => s.Kind == MarkdownSpanKind.WikiLink);
    }

    [Fact]
    public void Tokenizer_Highlights_CSharp_Keywords()
    {
        var tokens = CodeTokenizer.Tokenize("var x = 1; // hi", "csharp");
        Assert.Contains(tokens, t => t.Kind == CodeTokenKind.Keyword);
        Assert.Contains(tokens, t => t.Kind == CodeTokenKind.Comment);
        Assert.Contains(tokens, t => t.Kind == CodeTokenKind.Number);
    }

    [Fact]
    public void Tokenizer_Highlights_Rust_And_Bash()
    {
        var rust = CodeTokenizer.Tokenize("fn main() { let x = 1; }", "rust");
        Assert.Contains(rust, t => t.Kind == CodeTokenKind.Keyword);
        var bash = CodeTokenizer.Tokenize("if true; then echo hi; fi", "bash");
        Assert.Contains(bash, t => t.Kind == CodeTokenKind.Keyword);
    }

    [Fact]
    public void Css_Url_Is_Not_A_Line_Comment()
    {
        var code = "/* c */ a { b: url(http://x); }";
        var tokens = CodeTokenizer.Tokenize(code, "css");
        Assert.Contains(tokens, t => t.Kind == CodeTokenKind.Comment && code.Substring(t.Start, t.Length).Contains('c'));
        Assert.DoesNotContain(tokens, t => t.Kind == CodeTokenKind.Comment && code.Substring(t.Start, t.Length).Contains("http", StringComparison.Ordinal));
    }
}

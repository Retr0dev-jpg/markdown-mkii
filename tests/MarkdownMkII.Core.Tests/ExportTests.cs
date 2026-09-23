using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Services;

namespace MarkdownMkII.Core.Tests;

public class ExportTests
{
    [Fact]
    public void Html_Contains_Title_And_Body()
    {
        var html = ExportHtmlService.Export("# Ciao\n\nTesto", "Documento");
        Assert.Contains("<title>Documento</title>", html);
        Assert.Contains("<h1", html);
        Assert.Contains("Testo", html);
        Assert.Contains("markdown-body", html);
        Assert.Contains("<nav class=\"toc\"", html);
        Assert.Contains("href=\"#", html);
    }

    [Fact]
    public void Html_Can_Omit_Toc()
    {
        var html = ExportHtmlService.Export("# Ciao\n\nTesto", "Documento", new ExportSettings { IncludeToc = false });
        Assert.DoesNotContain("<nav class=\"toc\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_Highlights_Fenced_CSharp()
    {
        var html = ExportHtmlService.Export("```csharp\nvar x = 1;\n```");
        Assert.Contains("tok-keyword", html);
        Assert.Contains("language-csharp", html);
    }
}

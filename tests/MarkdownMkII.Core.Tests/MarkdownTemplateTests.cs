using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class MarkdownTemplateTests
{
    [Fact]
    public void Blank_And_Starter_Include_Title()
    {
        Assert.Equal("# Hello\n\n", MarkdownTemplates.Blank("Hello"));
        var starter = MarkdownTemplates.Starter("Hello");
        Assert.Contains("title: Hello", starter);
        Assert.Contains("# Hello", starter);
        Assert.Contains("- [ ]", starter);
        Assert.Contains("```csharp", starter);
        var daily = MarkdownTemplates.Daily("2026-09-10");
        Assert.Contains("date: ", daily);
        Assert.Contains("tags: daily", daily);
        Assert.Equal(MarkdownTemplates.StarterId, MarkdownTemplates.NormalizeId("nope"));
        Assert.Contains("Partecipanti", MarkdownTemplates.Meeting("Sync"));
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), MarkdownTemplates.SuggestedTitle("daily", "x"));
    }
}

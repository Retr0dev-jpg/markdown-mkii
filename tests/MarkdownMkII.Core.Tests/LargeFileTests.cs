namespace MarkdownMkII.Core.Tests;

public class LargeFileTests
{
    [Fact]
    public void Detects_Large_By_Line_Count()
    {
        var lines = string.Join('\n', Enumerable.Repeat("x", DocumentParser.LargeFileLines));
        Assert.True(DocumentParser.IsLarge(lines));
        Assert.False(DocumentParser.IsLarge("piccolo"));
    }
}

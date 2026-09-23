using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class LineMapTests
{
    [Fact]
    public void Maps_Offsets_And_Lines()
    {
        var map = new LineMap("a\nbc\n\ndef");
        Assert.Equal(4, map.LineCount);
        Assert.Equal(0, map.LineOfOffset(0));
        Assert.Equal("bc", map.LineText(1));
        Assert.Equal(1, map.LineOfOffset(4));
        Assert.Equal(2, map.LineOfOffset(5));
        Assert.Equal("def", map.LineText(3));
    }
}

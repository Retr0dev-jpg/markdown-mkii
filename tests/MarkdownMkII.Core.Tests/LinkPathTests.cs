using MarkdownMkII.Core.Preview;

namespace MarkdownMkII.Core.Tests;

public class LinkPathTests
{
    [Fact]
    public void Relative_Path_Uses_Document_Folder()
    {
        var relative = LinkResolver.ToMarkdownRelativePath("/tmp/notes/doc.md", "/tmp/notes/img/pic.png");
        Assert.Equal("./img/pic.png", relative.Replace('\\', '/'));
    }
}

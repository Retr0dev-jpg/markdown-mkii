using MarkdownMkII.Core.Services;

namespace MarkdownMkII.Core.Tests;

public class DocumentStorePathTests
{
    [Fact]
    public void UniqueMarkdownPath_Increments_And_Sanitizes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-uniq-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var first = DocumentStore.UniqueMarkdownPath(dir, "note");
            File.WriteAllText(first, "a");
            var second = DocumentStore.UniqueMarkdownPath(dir, "note");
            Assert.NotEqual(first, second);
            Assert.Equal("note 2.md", Path.GetFileName(second));
            var nested = DocumentStore.UniqueMarkdownPath(dir, "a/b");
            Assert.Equal("a-b.md", Path.GetFileName(nested));
            Assert.Equal(Path.GetFullPath(dir), Path.GetFullPath(Path.GetDirectoryName(nested)!));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

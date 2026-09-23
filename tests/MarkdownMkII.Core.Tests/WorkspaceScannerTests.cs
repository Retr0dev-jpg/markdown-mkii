using MarkdownMkII.Core.Services;

namespace MarkdownMkII.Core.Tests;

public class WorkspaceScannerTests
{
    [Fact]
    public void Scans_Markdown_Only()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-ws-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        File.WriteAllText(Path.Combine(dir, "a.md"), "a");
        File.WriteAllText(Path.Combine(dir, "b.txt"), "b");
        File.WriteAllText(Path.Combine(dir, "skip.bin"), "x");
        File.WriteAllText(Path.Combine(dir, "sub", "c.markdown"), "hello from c\nmore hello from c");
        try
        {
            var files = WorkspaceScanner.EnumerateMarkdownFiles(dir);
            Assert.Equal(3, files.Count);
            var tree = WorkspaceScanner.Scan(dir);
            Assert.True(tree.IsDirectory);
            Assert.Contains(tree.Children, c => c.Name == "a.md");
            var filtered = WorkspaceScanner.Filter(tree, "c.mark");
            Assert.Contains(filtered.Children, c => c.IsDirectory && c.Name == "sub");
            Assert.Contains(filtered.Children.Single(c => c.IsDirectory).Children, c => c.Name == "c.markdown");
            Assert.DoesNotContain(filtered.Children, c => c.Name == "a.md");
            var hits = WorkspaceScanner.SearchContent([dir], "hello from c");
            Assert.Contains(hits, hit => hit.Name == "c.markdown" && hit.Line == 1);
            Assert.Contains(hits, hit => hit.Name == "c.markdown" && hit.Line == 2);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

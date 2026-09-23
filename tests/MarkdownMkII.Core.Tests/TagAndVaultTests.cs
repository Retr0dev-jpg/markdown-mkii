using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class TagAndVaultTests
{
    [Fact]
    public void Extracts_Hash_Tags_And_Front_Matter()
    {
        var tags = MarkdownTags.Extract(
            "---\ntags: alpha, beta\n---\n# Title\n\nSee #gamma and #gamma.\n\n```\n#notatag\n```\n",
            new Dictionary<string, string> { ["tags"] = "alpha, beta" });
        Assert.Contains("alpha", tags);
        Assert.Contains("beta", tags);
        Assert.Contains("gamma", tags);
        Assert.DoesNotContain("notatag", tags, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Title", tags, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Replaces_Across_Vault_Files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-vault-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.md"), "alpha one");
            File.WriteAllText(Path.Combine(dir, "b.md"), "alpha two");
            File.WriteAllText(Path.Combine(dir, "c.md"), "gamma");
            var files = WorkspaceScanner.ReplaceContent([dir], "alpha", "beta");
            Assert.Equal(2, files);
            Assert.Contains("beta one", File.ReadAllText(Path.Combine(dir, "a.md")));
            Assert.Contains("gamma", File.ReadAllText(Path.Combine(dir, "c.md")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

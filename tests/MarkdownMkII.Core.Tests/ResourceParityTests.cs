namespace MarkdownMkII.Core.Tests;

public class ResourceParityTests
{
    [Fact]
    public void Italian_And_English_Share_The_Same_Keys()
    {
        var root = FindRepoRoot();
        var italian = ReadKeys(Path.Combine(root, "src", "MarkdownMkII.App", "Strings", "it", "Resources.resw"));
        var english = ReadKeys(Path.Combine(root, "src", "MarkdownMkII.App", "Strings", "en-US", "Resources.resw"));
        Assert.True(italian.Count > 20);
        Assert.Equal(italian, english);
    }

    private static HashSet<string> ReadKeys(string path)
    {
        var xml = System.Xml.Linq.XDocument.Load(path);
        return xml.Root!
            .Elements("data")
            .Select(element => (string?)element.Attribute("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MarkdownMkII.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("MarkdownMkII.slnx");
    }
}

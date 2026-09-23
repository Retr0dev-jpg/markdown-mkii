using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class FrontMatterTests
{
    [Fact]
    public void Parses_Simple_Yaml()
    {
        Assert.True(FrontMatter.TryParse("title: Hello\nauthor: 'Ada'\n", out var fields));
        Assert.Equal("Hello", fields["title"]);
        Assert.Equal("Ada", fields["author"]);
    }

    [Fact]
    public void Upsert_Inserts_And_Updates_Fields()
    {
        var inserted = FrontMatter.Upsert("# Hello\n", "title", "Note");
        Assert.StartsWith("---\ntitle: Note\n---", inserted);
        Assert.Contains("# Hello", inserted);
        var updated = FrontMatter.Upsert("---\ntitle: Old\n---\n# Body\n", "title", "New");
        Assert.Contains("title: New", updated);
        Assert.Contains("# Body", updated);
        Assert.DoesNotContain("title: Old", updated);
    }

    private const string Rich = "---\ntitle: \"Hello: world\"\ndescription: |\n  Keep this\n  hello: world\n# comment\ntags:\n  - one\n  - two\n---\n\nBody\n";

    [Fact]
    public void Upsert_Preserves_Every_Other_Line()
    {
        var next = FrontMatter.EnsureField(Rich, "project", "x");
        Assert.Equal(Rich.Replace("  - two\n---", "  - two\nproject: x\n---"), next);
    }

    [Fact]
    public void Parse_Reads_Block_Scalars_And_Lists_Without_Inventing_Keys()
    {
        Assert.True(FrontMatter.TrySplit(Rich, out var fields, out var body));
        Assert.Equal("Hello: world", fields["title"]);
        Assert.Equal("Keep this\nhello: world", fields["description"]);
        Assert.False(fields.ContainsKey("hello"));
        Assert.Equal(["one", "two"], FrontMatter.List(fields, "tags"));
        Assert.Equal("Body\n", body);
    }

    [Fact]
    public void Updating_A_Multiline_Value_Replaces_Only_That_Entry()
    {
        var next = FrontMatter.Upsert(Rich, "description", "short");
        Assert.Contains("description: short\n# comment\ntags:\n  - one", next);
        Assert.DoesNotContain("Keep this", next);
        Assert.EndsWith("---\n\nBody\n", next);
    }

    [Fact]
    public void Leading_Thematic_Break_Keeps_The_Body()
    {
        const string text = "---\n\nIntro: still the note\n\n---\n\nAfter";
        var next = FrontMatter.EnsureField(text, "status", "draft");
        Assert.Contains("Intro: still the note", next);
        Assert.EndsWith("---\n\nAfter", next);
    }

    [Fact]
    public void Lists_Respect_Quotes()
    {
        var next = FrontMatter.EnsureTags("---\ntags: [\"red, blue\", green]\n---\n", "new");
        Assert.Contains("tags: [\"red, blue\", green, new]", next);
        FrontMatter.TrySplit(next, out var fields, out _);
        Assert.Equal(["red, blue", "green", "new"], FrontMatter.List(fields, "tags"));
    }

    [Fact]
    public void Sort_Rename_And_Remove_Keep_Values_And_Comments()
    {
        const string text = "---\n# about b\nb: 2\na: |\n  line\n---\nBody";
        Assert.Equal("---\na: |\n  line\n# about b\nb: 2\n---\nBody", FrontMatter.SortKeys(text));
        Assert.Equal("---\n# about b\nb: 2\nz: |\n  line\n---\nBody", FrontMatter.RenameKey(text, "a", "z"));
        Assert.Equal("---\n# about b\nb: 2\n---\nBody", FrontMatter.RemoveKey(text, "a"));
    }

    [Fact]
    public void Values_That_Yaml_Would_Misread_Are_Quoted_And_Read_Back()
    {
        var next = FrontMatter.Upsert("# Note\n", "title", "Ratio: 3 #1");
        Assert.StartsWith("---\ntitle: \"Ratio: 3 #1\"\n---", next);
        FrontMatter.TrySplit(next, out var fields, out _);
        Assert.Equal("Ratio: 3 #1", fields["title"]);
    }
}

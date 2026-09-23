using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class WikiIndexTests
{
    [Fact]
    public void Rewrites_Targets_And_Finds_Backlinks()
    {
        Assert.Equal("See [[Bar|Label]] and [[Bar]]", WikiLinks.RewriteTarget("See [[Foo|Label]] and [[Foo]]", "Foo", "Bar"));
        Assert.Equal("[[Foo-note]]", WikiLinks.RewriteTarget("[[Foo-note]]", "Foo", "Bar"));
        Assert.Equal("[[Bar#Hi|L]]", WikiLinks.RewriteTarget("[[Foo#Hi|L]]", "Foo", "Bar"));
        Assert.Equal("[x](Bar.md#h)", WikiLinks.RewriteMarkdownFileLinks("[x](Foo.md#h)", "Foo.md", "Bar.md"));

        var hits = WikiLinks.Find("See [[note#Hello|Hi]] and ![[other]] and [[#Local]]").ToList();
        Assert.Equal("note", hits[0].Target);
        Assert.Equal("Hello", hits[0].Heading);
        Assert.Equal("Hi", hits[0].Label);
        Assert.True(hits[1].IsEmbed);
        Assert.Equal(string.Empty, hits[2].Target);
        Assert.Equal("Local", hits[2].Heading);

        var dir = Path.Combine(Path.GetTempPath(), "mkii-wiki-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Alpha.md"), "See [[Beta]] here");
            File.WriteAllText(Path.Combine(dir, "Beta.md"), "root");
            File.WriteAllText(Path.Combine(dir, "Gamma.md"), "Also [[Beta|B]]");
            var backlinks = WikiIndex.FindBacklinks([dir], "Beta", Path.Combine(dir, "Beta.md"));
            Assert.Equal(2, backlinks.Count);
            Assert.Equal(2, WikiIndex.RewriteAll([dir], "Beta", "Delta"));
            Assert.Contains("[[Delta]]", File.ReadAllText(Path.Combine(dir, "Alpha.md")));
            Assert.Contains("[[Delta|B]]", File.ReadAllText(Path.Combine(dir, "Gamma.md")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Embeds_Inline_Note_And_Heading_Section()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-embed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var target = Path.Combine(dir, "notes.md");
            var source = Path.Combine(dir, "doc.md");
            File.WriteAllText(target, "# Hello\n\nInside hello\n\n# Other\n\nSkip me\n");
            File.WriteAllText(source, "Intro\n\n![[notes#Hello]]\n");
            var parsed = DocumentParser.Parse(File.ReadAllText(source), source, vaultFolders: [dir]);
            var embed = parsed.Preview.Blocks
                .OfType<ParagraphBlockIr>()
                .SelectMany(block => block.Inlines)
                .OfType<WikiEmbedInline>()
                .Single();
            Assert.False(embed.Missing);
            Assert.Contains(embed.Children, child => child is HeadingBlockIr { Level: 1 });
            Assert.DoesNotContain(
                embed.Children.OfType<ParagraphBlockIr>(),
                child => child.Inlines.OfType<TextInline>().Any(text => text.Text.Contains("Skip me")));
            Assert.Contains("Inside hello", WikiIndex.ExtractHeadingSection(File.ReadAllText(target), "Hello"));
            Assert.DoesNotContain("Skip me", WikiIndex.ExtractHeadingSection(File.ReadAllText(target), "Hello"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Unresolved_And_Tags_Scan_Vault()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-unresolved-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "Alpha.md"), "See [[Missing]] and #inbox\n");
            File.WriteAllText(Path.Combine(dir, "Beta.md"), "root #done\n");
            File.WriteAllText(Path.Combine(dir, "Gamma.md"), "See [[Beta]]\n");
            File.WriteAllText(Path.Combine(dir, "Delta.md"), "I mentioned Beta in prose\n");
            var unresolved = WikiIndex.Unresolved([dir]);
            Assert.Contains(unresolved, hit => hit.Preview.Contains("[[Missing]]", StringComparison.Ordinal));
            Assert.DoesNotContain(unresolved, hit => hit.Preview.Contains("[[Beta]]", StringComparison.Ordinal));
            var tags = WikiIndex.AllTags([dir]);
            Assert.Contains("inbox", tags);
            Assert.Contains("done", tags);
            Assert.Contains("Beta", WikiIndex.Catalog([dir]));
            var tagged = WikiIndex.FilesWithTag([dir], "inbox");
            Assert.Contains(tagged, hit => hit.Name.Equals("Alpha.md", StringComparison.OrdinalIgnoreCase));
            var outgoing = WikiIndex.Outgoing(File.ReadAllText(Path.Combine(dir, "Alpha.md")), Path.Combine(dir, "Alpha.md"), [dir]);
            Assert.Contains(outgoing, hit => hit.Name.Equals("Missing", StringComparison.OrdinalIgnoreCase));
            var orphans = WikiIndex.Orphans([dir]);
            Assert.Contains(orphans, hit => hit.Name.Equals("Alpha.md", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(orphans, hit => hit.Name.Equals("Beta.md", StringComparison.OrdinalIgnoreCase));
            var graph = WikiIndex.Graph([dir]);
            Assert.Contains(graph.Nodes, node => node.Stem.Equals("Beta", StringComparison.OrdinalIgnoreCase) && node.Incoming >= 1);
            Assert.Contains(graph.Edges, edge =>
                edge.FromStem.Equals("Gamma", StringComparison.OrdinalIgnoreCase) &&
                edge.ToStem.Equals("Beta", StringComparison.OrdinalIgnoreCase) &&
                edge.Resolved);
            Assert.Contains(graph.Edges, edge =>
                edge.FromStem.Equals("Alpha", StringComparison.OrdinalIgnoreCase) &&
                edge.ToStem.Equals("Missing", StringComparison.OrdinalIgnoreCase) &&
                !edge.Resolved);
            var unlinked = WikiIndex.UnlinkedMentions([dir]);
            Assert.Contains(unlinked, hit =>
                hit.Name.Equals("Delta.md", StringComparison.OrdinalIgnoreCase) &&
                hit.Preview.Equals("Beta", StringComparison.OrdinalIgnoreCase));
            var vault = MarkdownDiagnostics.AnalyzeVault([dir]);
            Assert.Contains(vault, issue => issue.Issue.Code == "wiki");
            File.WriteAllText(Path.Combine(dir, "2026-09-10.md"), MarkdownTemplates.Daily("2026-09-10"));
            var dailies = WikiIndex.DailyNotes([dir]);
            Assert.Contains(dailies, hit => hit.Name.Equals("2026-09-10.md", StringComparison.OrdinalIgnoreCase));
            Assert.True(WikiIndex.IsDailyStem("2026-09-10"));
            Assert.False(WikiIndex.IsDailyStem("note"));
            Assert.Equal(
                Path.Combine(dir, "2026-09-10.md"),
                WikiIndex.ExistingDaily([dir], new DateTime(2026, 9, 10)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WikiLinks_AtCaret_And_MarkdownLinkAt()
    {
        var wiki = "See [[note#Hello|Hi]] here";
        var hit = WikiLinks.AtCaret(wiki, wiki.IndexOf("note", StringComparison.Ordinal));
        Assert.NotNull(hit);
        Assert.Equal("note", hit.Value.Target);
        Assert.Equal("Hello", hit.Value.Heading);
        Assert.Null(WikiLinks.AtCaret(wiki, 0));

        var md = "a [x](foo.md#h) b";
        var link = WikiLinks.MarkdownLinkAt(md, md.IndexOf("foo", StringComparison.Ordinal));
        Assert.NotNull(link);
        Assert.Equal("foo.md#h", link.Value.Url);
        Assert.Null(WikiLinks.MarkdownLinkAt("plain", 0));
    }
}

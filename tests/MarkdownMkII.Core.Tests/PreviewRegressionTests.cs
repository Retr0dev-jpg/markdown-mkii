using MarkdownMkII.Core.Preview;

namespace MarkdownMkII.Core.Tests;

public class PreviewRegressionTests
{
    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void LargeFileThresholdCountsLogicalLines(string newline)
    {
        Assert.True(MarkdownMkII.Core.DocumentParser.IsLarge(string.Concat(Enumerable.Repeat(newline, 9999))));
        Assert.False(MarkdownMkII.Core.DocumentParser.IsLarge(string.Concat(Enumerable.Repeat(newline, 9998))));
    }

    [Fact]
    public void NamedFootnoteRetainsItsSourceLabel()
    {
        var preview = PreviewBuilder.Build("Read this[^source].\n\n[^source]: The reference.\n");
        var paragraph = Assert.IsType<ParagraphBlockIr>(preview.Blocks[0]);
        Assert.Equal("source", Assert.Single(paragraph.Inlines.OfType<FootnoteRefInline>()).Label);
        var definitions = Assert.IsType<QuoteBlockIr>(preview.Blocks[1]);
        var footnote = Assert.IsType<FootnoteBlockIr>(Assert.Single(definitions.Children));
        Assert.DoesNotContain(Assert.IsType<ParagraphBlockIr>(footnote.Children[0]).Inlines, item => item is FootnoteRefInline);
    }

    [Fact]
    public void AutolinkEmailKeepsMailtoScheme()
    {
        var paragraph = Assert.IsType<ParagraphBlockIr>(Assert.Single(PreviewBuilder.Build("<reader@example.com>").Blocks));
        Assert.Equal("mailto:reader@example.com", Assert.IsType<LinkInline>(Assert.Single(paragraph.Inlines)).Url);
    }

    [Fact]
    public void SourceRangesIncludeFenceDelimitersAndMultilineNestedContent()
    {
        var preview = PreviewBuilder.Build("```csharp\nvar x = 1;\n```\n\n> first\n> second\n> third\n");
        Assert.Equal(2, preview.Blocks[0].SourceEndLine);
        Assert.Equal(6, preview.Blocks[1].SourceEndLine);
    }

    [Fact]
    public void SelfHeadingEmbedUsesUnsavedDocumentSnapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), "mkii-self-" + Guid.NewGuid().ToString("N") + ".md");
        var preview = PreviewBuilder.Build("# Source\nCurrent unsaved content\n\n# References\n![[#Source]]\n", path);
        var paragraph = Assert.IsType<ParagraphBlockIr>(preview.Blocks[^1]);
        var embed = Assert.IsType<WikiEmbedInline>(Assert.Single(paragraph.Inlines));
        Assert.False(embed.Missing);
        Assert.Contains(embed.Children.OfType<ParagraphBlockIr>().SelectMany(item => item.Inlines),
            item => item is TextInline { Text: "Current unsaved content" });
    }

    [Fact]
    public void MediaEmbedIsAnImageAndNestedNoteEmbedKeepsItsDirectory()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mkii-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "nested"));
        try
        {
            File.WriteAllText(Path.Combine(folder, "nested", "note.md"), "Nested content");
            File.WriteAllText(Path.Combine(folder, "note.md"), "Wrong content");
            File.WriteAllText(Path.Combine(folder, "picture.png"), "test");
            var document = Path.Combine(folder, "current.md");
            var preview = PreviewBuilder.Build("![[picture.png]]\n\n![[nested/note]]", document, [folder]);
            var image = Assert.IsType<LinkInline>(Assert.Single(Assert.IsType<ParagraphBlockIr>(preview.Blocks[0]).Inlines));
            Assert.True(image.IsImage);
            Assert.Equal(Path.Combine(folder, "picture.png"), image.Url);
            var embed = Assert.IsType<WikiEmbedInline>(Assert.Single(Assert.IsType<ParagraphBlockIr>(preview.Blocks[1]).Inlines));
            Assert.Equal(Path.Combine(folder, "nested", "note.md"), embed.Url);
            Assert.Equal("Nested content", Assert.IsType<TextInline>(Assert.Single(Assert.IsType<ParagraphBlockIr>(embed.Children[0]).Inlines)).Text);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void SetextPreviewRangeStartsAtTheFirstTitleLine(string newline)
    {
        var source = string.Join(newline, "Introduction", "", "First title line", "second title line", "================", "", "Body");
        var preview = PreviewBuilder.Build(source);
        var heading = Assert.Single(preview.Blocks.OfType<HeadingBlockIr>());
        Assert.Equal(2, heading.SourceLine);
        Assert.Equal(4, heading.SourceEndLine);
    }

    [Fact]
    public void NestedSelfHeadingEmbedCanReadSiblingFromTheFullUnsavedSnapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), "mkii-self-nested-" + Guid.NewGuid().ToString("N") + ".md");
        var preview = PreviewBuilder.Build("# Container\n![[#Sibling]]\n\n# Sibling\nUnsaved sibling content\n\n# Entry\n![[#Container]]\n", path);
        var container = Assert.IsType<WikiEmbedInline>(Assert.Single(Assert.IsType<ParagraphBlockIr>(preview.Blocks[^1]).Inlines));
        var sibling = SingleEmbed(container.Children);
        Assert.False(sibling.Missing);
        Assert.Contains(sibling.Children.OfType<ParagraphBlockIr>().SelectMany(block => block.Inlines),
            inline => inline is TextInline { Text: "Unsaved sibling content" });
        Assert.DoesNotContain(sibling.Children.OfType<ParagraphBlockIr>().SelectMany(block => block.Inlines),
            inline => inline is WikiEmbedInline);
    }

    [Fact]
    public void ExternalSectionKeepsItsFullDocumentForNestedSiblingEmbeds()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mkii-external-section-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "other.md"), "# Section\n![[#Sibling]]\n\n# Sibling\nFull external snapshot\n");
            var preview = PreviewBuilder.Build("![[other#Section]]", Path.Combine(folder, "current.md"), [folder]);
            var section = SingleEmbed(preview.Blocks);
            var sibling = SingleEmbed(section.Children);
            Assert.False(sibling.Missing);
            Assert.Contains(sibling.Children.OfType<ParagraphBlockIr>().SelectMany(block => block.Inlines),
                inline => inline is TextInline { Text: "Full external snapshot" });
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void NestedExternalEmbedReturningToRootUsesUnsavedSnapshotInsteadOfDisk()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mkii-snapshot-return-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "nested"));
        try
        {
            File.WriteAllText(Path.Combine(folder, "root.md"), "# Live\nStale disk content\n");
            File.WriteAllText(Path.Combine(folder, "nested", "other.md"), "# Section\n![[../root#Live]]\n");
            var source = "# Live\nCurrent unsaved root content\n\n# Entry\n![[nested/other#Section]]\n";
            var preview = PreviewBuilder.Build(source, Path.Combine(folder, ".", "root.md"), [folder]);
            var external = Assert.IsType<WikiEmbedInline>(Assert.Single(Assert.IsType<ParagraphBlockIr>(preview.Blocks[^1]).Inlines));
            var returned = SingleEmbed(external.Children);
            Assert.False(returned.Missing);
            var text = returned.Children.OfType<ParagraphBlockIr>().SelectMany(block => block.Inlines).OfType<TextInline>().Select(inline => inline.Text).ToList();
            Assert.Contains("Current unsaved root content", text);
            Assert.DoesNotContain("Stale disk content", text);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static WikiEmbedInline SingleEmbed(IReadOnlyList<PreviewBlock> blocks)
        => Assert.Single(blocks.OfType<ParagraphBlockIr>().SelectMany(block => block.Inlines).OfType<WikiEmbedInline>());
}

using System.Collections.Specialized;
using System.Text.Json;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public sealed class WorkspaceRefactorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "markdown-mkii-refactor-" + Guid.NewGuid().ToString("N"));

    public WorkspaceRefactorTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    private string Note(string relative, string text)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void OverlappingFolders_DoNotDuplicateSearchOrMetadataHits()
    {
        var note = Note("nested/note.md", "---\ntitle: Alpha\n---\nneedle\n");
        string[] folders = [root, Path.Combine(root, "nested"), root];
        Assert.Equal(note, Assert.Single(WorkspaceScanner.SearchContent(folders, "needle")).Path);
        Assert.Equal(note, Assert.Single(WorkspaceScanner.SearchRegex(folders, "n.edle")).Path);
        Assert.Equal(note, Assert.Single(WikiIndex.NotesWithoutDescription(folders)).Path);
        Assert.Equal(note, Assert.Single(WikiIndex.NotesWithoutField(folders, "description")).Path);
    }

    [Fact]
    public void MissingMetadata_RequiresFrontMatterAndDistinguishesPopulatedFields()
    {
        var missing = Note("missing.md", "---\ntitle: Missing\n---\n");
        Note("present.md", "---\ndescription: Present\n---\n");
        Note("body.md", "No front matter.");
        Assert.Equal(missing, Assert.Single(WikiIndex.NotesWithoutField([root], "description")).Path);
    }

    [Theory]
    [InlineData("label = 'Research AND Development' AND id IS PMID", true)]
    [InlineData("label = 'Research OR Development' OR id IS NOT EMPTY", true)]
    [InlineData("(label = 'Research AND Development') AND id IS PMID", true)]
    [InlineData("id IS EMAIL", false)]
    [InlineData("id IS 999", false)]
    [InlineData("absent IS EMPTY", true)]
    [InlineData("id IS NOT EMPTY", true)]
    public void MetadataQueries_HandleTypesAndQuotedBooleanWords(string query, bool matches)
    {
        Note("note.md", "---\nlabel: Research AND Development\nid: 12345678\n---\n");
        Assert.Equal(matches ? 1 : 0, WikiIndex.QueryFrontMatter([root], query).Count);
    }

    [Theory]
    [InlineData(TextEncodingKind.Utf8Bom)]
    [InlineData(TextEncodingKind.Utf16Le)]
    [InlineData(TextEncodingKind.Utf16Be)]
    public void WorkspaceReplace_PreservesEncodingAndNewlines(TextEncodingKind encoding)
    {
        var path = Path.Combine(root, "note.md");
        var profile = new TextFileProfile(encoding, NewlineKind.Crlf);
        File.WriteAllBytes(path, TextFileCodec.Encode("old\nsecond\n", profile));
        Assert.Equal(1, WorkspaceScanner.ReplaceContent([root], "old", "new"));
        var decoded = TextFileCodec.Decode(File.ReadAllBytes(path));
        Assert.Equal(profile, decoded.Profile);
        Assert.Equal("new\nsecond\n", decoded.Text);
        Assert.Equal(0, WorkspaceScanner.ReplaceContent([root], "new", "new"));
    }

    [Fact]
    public void TagRewrite_SkipsOpenDocumentsAndPreservesOtherFiles()
    {
        var open = Note("open.md", "#old");
        var closed = Note("closed.md", "#old");
        Assert.Equal(1, WikiIndex.RewriteTag([root], "old", "new", [open]));
        Assert.Equal("#old", File.ReadAllText(open));
        Assert.Equal("#new", File.ReadAllText(closed));
    }

    [Fact]
    public void Scan_AndEnumeration_HonorAlreadyCancelledToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => WorkspaceScanner.Scan(root, cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => WorkspaceScanner.EnumerateMarkdownFiles(root, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void UniqueNames_AlsoAvoidDirectoryCollisions()
    {
        Directory.CreateDirectory(Path.Combine(root, "note.md"));
        Directory.CreateDirectory(Path.Combine(root, "picture.png"));
        Assert.Equal(Path.Combine(root, "note 2.md"), DocumentStore.UniqueMarkdownPath(root, "note"));
        Assert.Equal(Path.Combine(root, "picture-2.png"), VaultAttachments.UniquePath(root, "picture", ".png"));
    }

    [Fact]
    public void GraphJson_EscapesAllControlCharacters()
    {
        const string stem = "quote\"\tline\r\n\u0001";
        var graph = new WikiGraph([new("note.md", stem, 1, 0)], [new(stem, stem, true)]);
        using var parsed = JsonDocument.Parse(WikiIndex.ToGraphJson(graph));
        Assert.Equal(stem, parsed.RootElement.GetProperty("nodes")[0].GetProperty("stem").GetString());
    }

    [Fact]
    public void Graph_ResolvesAliasesAndFileExtensionsToTheSameNode()
    {
        Note("Target.md", "---\naliases: [Alias]\n---\n# Target");
        Note("Source.md", "[[Alias]] [[Target.md]]");
        var graph = WikiIndex.Graph([root]);
        Assert.Equal(1, Assert.Single(graph.Nodes, node => node.Stem == "Target").Incoming);
        var edge = Assert.Single(graph.Edges);
        Assert.Equal("Target", edge.ToStem);
        Assert.True(edge.Resolved);
        var ego = WikiIndex.Ego([root], "Source", maxNodes: 1);
        Assert.Single(ego.Nodes);
        Assert.Empty(ego.Edges);
    }

    [Fact]
    public void Backlinks_ResolveSameNamedNotesRelativeToEachSource()
    {
        var target = Note("a/Target.md", "target");
        var linked = Note("a/Source.md", "[[Target]]");
        Note("b/Target.md", "other target");
        Note("b/Source.md", "[[Target]]");
        Assert.Equal(linked, Assert.Single(WikiIndex.FindBacklinks([root], "Target", target)).Path);
    }

    [Fact]
    public void RelocateToCurrentFolder_DoesNotRenameTheNote()
    {
        var note = Note("note.md", "content");
        Assert.Equal(note, WikiIndex.Relocate(note, root, [root]));
        Assert.Single(Directory.GetFiles(root));
    }

    [Fact]
    public void AssetExport_OnlyChangesUrlsAndHandlesSameBasenames()
    {
        var note = Note("note.md", "");
        Note("one/picture.png", "one");
        Note("two/picture.png", "two");
        var html = Path.Combine(root, "export", "page.html");
        const string markdown = "# picture.png\n\npicture.png prose\n\n![first](one/picture.png)\n![second](two/picture.png)\n![again](one/picture.png)";
        Assert.Equal(2, ExportHtmlService.ExportWithAssets(markdown, html, note, [root]));
        var result = File.ReadAllText(html);
        Assert.Contains("picture.png prose", result);
        Assert.Contains("src=\"assets/picture.png\"", result);
        Assert.Contains("src=\"assets/picture-2.png\"", result);
        Assert.DoesNotContain("assets/assets/", result);
        Assert.Equal("one", File.ReadAllText(Path.Combine(root, "export/assets/picture.png")));
        Assert.Equal("two", File.ReadAllText(Path.Combine(root, "export/assets/picture-2.png")));
    }

    [Fact]
    public void AssetExport_ResolvesEscapedFilenamesAndLeavesRemoteImages()
    {
        var note = Note("note.md", "");
        Note("a b.png", "image");
        const string markdown = "![local](a%20b.png)\n![remote](https://example.test/a%20b.png)";
        var html = Path.Combine(root, "export/page.html");
        Assert.Single(ExportHtmlService.CollectLocalAssets(markdown, note, [root]));
        Assert.Equal(1, ExportHtmlService.ExportWithAssets(markdown, html, note, [root]));
        Assert.Contains("src=\"assets/a%20b.png\"", File.ReadAllText(html));
        Assert.Contains("https://example.test/a%20b.png", File.ReadAllText(html));
    }

    [Fact]
    public void MarkdownExport_DoesNotRecopyPreviousExportsInsideTheVault()
    {
        Note("note.md", "note");
        Note("export/previous.md", "previous");
        var destination = Path.Combine(root, "export");
        Assert.Equal(1, ExportHtmlService.ExportMarkdownVault([root], destination));
        Assert.False(Directory.Exists(Path.Combine(destination, "export")));
    }

    [Fact]
    public void HtmlExport_DoesNotOverwriteAnIndexNoteWithTheGeneratedDirectory()
    {
        Note("source/index.md", "# My actual note");
        var destination = Path.Combine(root, "export");
        Assert.Equal(1, ExportHtmlService.ExportVault([Path.Combine(root, "source")], destination));
        Assert.Contains("My actual note", File.ReadAllText(Path.Combine(destination, "index.html")));
    }

    [Fact]
    public void MarkdownHrefRewrite_DoesNotDoubleEncodeHtmlEntities()
    {
        Assert.Equal("href=\"A&amp;B.html#one&amp;two\"",
            ExportHtmlService.RewriteMarkdownHrefs("href=\"A&amp;B.md#one&amp;two\""));
    }
}

public sealed class WikiCompletionRefactorTests
{
    [Fact]
    public void HeadingLookup_UsesMarkdownOutlineIncludingSetextAndExcludingCode()
    {
        const string markdown = "```md\n# Example\n```\n\nReal title\n==========\n\n## Child **heading**\ntext\n\n# Next\nend";
        Assert.Equal("Real title", WikiIndex.NoteTitle("note.md", markdown));
        Assert.False(WikiIndex.ContainsHeading(markdown, "Example"));
        Assert.True(WikiIndex.ContainsHeading(markdown, "child-heading"));
        var section = WikiIndex.ExtractHeadingSection(markdown, "real-title");
        Assert.StartsWith("Real title\n", section);
        Assert.Contains("## Child **heading**", section);
        Assert.DoesNotContain("# Next", section);
    }

    [Fact]
    public void Linkify_IgnoresMismatchedAndUnclosedFences()
    {
        const string markdown = "````md\n```\nTarget\n````\nTarget\n~~~md\nTarget";
        Assert.Equal("````md\n```\nTarget\n````\n[[Target]]\n~~~md\nTarget", WikiIndex.Linkify(markdown, "Target"));
    }

    [Fact]
    public void PartialTarget_IsSafeAtEveryCaretPosition()
    {
        const string text = "[[note#Heading|Alias]] tail";
        for (var caret = 0; caret <= text.Length; caret++)
        {
            var result = WikiSuggest.PartialTarget(text, caret);
            if (caret < 2 || caret > text.IndexOf("]]", StringComparison.Ordinal)) Assert.Null(result);
        }
        Assert.Null(WikiSuggest.PartialTarget(string.Empty, 0));
    }

    [Fact]
    public void Completion_PreservesBothHeadingAndAlias()
    {
        const string text = "[[old#Heading|Alias]]";
        Assert.Equal("old", WikiSuggest.PartialTarget(text, text.IndexOf("]]", StringComparison.Ordinal)));
        Assert.Equal("[[new#Heading|Alias]]", WikiSuggest.Complete(text, 5, "new").Text);
    }

    [Fact]
    public void Completion_AfterClosedLinkDoesNotRewriteEarlierContent()
    {
        const string text = "[[first]] tail";
        Assert.StartsWith(text, WikiSuggest.Complete(text, text.Length, "second").Text);
    }

    [Fact]
    public void Completion_DoesNotConsumeAFollowingLine()
    {
        const string text = "[[first\nother text]]";
        Assert.Equal("[[second]]\nother text]]", WikiSuggest.Complete(text, 7, "second").Text);
        Assert.Null(WikiSuggest.PartialTarget("[[first\rnext", 12));
    }

    [Fact]
    public void HeadingOnlyLinks_ConvertToLocalAnchors()
        => Assert.Equal("[Heading](#Heading)", WikiLinks.ToMarkdownLinks("[[#Heading]]"));

    [Fact]
    public void LinkConversion_PreservesRelativeDirectories()
        => Assert.Equal("[[notes/Target|Label]]", WikiLinks.ToWikilinks("[Label](notes/Target.md)"));

    [Fact]
    public void WikiLinkScanning_RejectsMultilineLinksAndRewritesTrimmedTargets()
    {
        Assert.Empty(WikiLinks.Find("[[First\nSecond]] [[First#Part\rOther]] [[First|One\nTwo]]"));
        Assert.Equal("![[New#Heading|Label]]", WikiLinks.RewriteTarget("![[ Old #Heading|Label]]", "Old", "New"));
    }

    [Fact]
    public void BulkReplace_SnapshotsLazySelfEnumerationAndSendsOneReset()
    {
        var collection = new BulkObservableCollection<int> { 1, 2, 3 };
        var notifications = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, args) => notifications.Add(args.Action);
        collection.ReplaceAll(collection.Where(value => value > 1));
        Assert.Equal([2, 3], collection);
        Assert.Equal([NotifyCollectionChangedAction.Reset], notifications);
    }

    [Fact]
    public void BulkReplace_FailedEnumerationLeavesOriginalCollectionIntact()
    {
        var collection = new BulkObservableCollection<int> { 1, 2 };
        Assert.Throws<InvalidOperationException>(() => collection.ReplaceAll(FailingSequence()));
        Assert.Equal([1, 2], collection);

        static IEnumerable<int> FailingSequence()
        {
            yield return 3;
            throw new InvalidOperationException("enumeration failed");
        }
    }
}

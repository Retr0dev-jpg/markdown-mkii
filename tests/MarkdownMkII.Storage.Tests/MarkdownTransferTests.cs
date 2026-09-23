using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage.Tests;
public sealed class MarkdownTransferTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mkii-transfer-" + Guid.NewGuid().ToString("N"));
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [Fact]
    public async Task ImportExportRoundtripPreservesOriginalsMetadataAssetsAndLinks()
    {
        var input = Path.Combine(root, "input");
        Directory.CreateDirectory(input);
        var a = Path.Combine(input, "A.md");
        var b = Path.Combine(input, "B.md");
        var original = "---\ntitle: Prima\ntags: [uno, due]\ncategory: Lavoro\ncustom:\n  nested: keep\n  list: [1, 2]\n---\n# Prima\n\n[[B|Seconda]]\n\n![foto](photo.png)\n\n`[[ignored]]`\n\n```md\n[[B]]\n```\n";
        await File.WriteAllTextAsync(a, original);
        await File.WriteAllTextAsync(b, "# Seconda\nCorpo");
        await File.WriteAllBytesAsync(Path.Combine(input, "photo.png"), [1, 2, 3, 4]);
        var db = new NoteDatabase(Path.Combine(root, "notes.db"));
        var transfer = new MarkdownTransfer(db);
        var imported = await transfer.ImportAsync(await MarkdownTransfer.InspectAsync([input]));
        Assert.Empty(imported.Warnings);
        Assert.Equal(2, imported.NoteIds.Count);
        Assert.Equal(original, await File.ReadAllTextAsync(a));
        var first = (await db.ResolveAsync("Prima")).Single();
        var second = (await db.ResolveAsync("Seconda")).Single();
        var note = (await db.GetAsync(first.Id))!;
        Assert.Contains("[Seconda](note://" + second.Id + ")", note.Markdown);
        Assert.Contains("attachment://", note.Markdown);
        Assert.Contains("```md\n[[B]]\n```", note.Markdown);
        Assert.Equal(new[] { "due", "uno" }, note.Summary.Tags);
        var output = Path.Combine(root, "output");
        await transfer.ExportAsync(imported.NoteIds, output);
        var exported = await File.ReadAllTextAsync(Path.Combine(output, "Prima.md"));
        Assert.Contains("custom:\n  nested: keep\n  list: [1, 2]", exported);
        Assert.Contains("[Seconda](Seconda.md)", exported);
        Assert.DoesNotContain("attachment://", exported);
        var other = new NoteDatabase(Path.Combine(root, "other.db"));
        var roundtrip = await new MarkdownTransfer(other).ImportAsync(await MarkdownTransfer.InspectAsync([output]));
        Assert.Empty(roundtrip.Warnings);
        var restored = (await other.GetAsync((await other.ResolveAsync("Prima")).Single().Id))!;
        Assert.Equal("Lavoro", restored.Summary.Category);
        Assert.Equal(note.Summary.Tags, restored.Summary.Tags);
        Assert.Equal(4, (await other.StatsAsync()).Attachments);
    }

    [Fact]
    public void ReferencesExcludeCodeAndNormalizeWikiWithoutDuplicates()
    {
        var references = NoteDatabase.ExtractLinks("[[Target#Heading|label]] `[[fake]]`\n\n```md\n[[fake]]\n```\n");
        var link = Assert.Single(references);
        Assert.Equal("Target", link.Target);
        Assert.Equal("label", link.Label);
    }

    [Fact]
    public async Task ImportReportsAmbiguousLinksAndMissingAssets()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "source.md");
        await File.WriteAllTextAsync(path, "[[Duplicate]]\n\n![missing](not-there.png)");
        var db = new NoteDatabase(Path.Combine(root, "notes.db"));
        await db.CreateAsync("Duplicate");
        await db.CreateAsync("Duplicate");
        var result = await new MarkdownTransfer(db).ImportAsync(await MarkdownTransfer.InspectAsync([path]));
        Assert.Contains(result.Warnings, w => w.Contains("Duplicate"));
        Assert.Contains(result.Warnings, w => w.Contains("not-there.png"));
    }

    [Fact]
    public async Task ExportDoesNotRewriteInternalUrlsInCodeExamples()
    {
        var db = new NoteDatabase(Path.Combine(root, "notes.db"));
        var target = await db.CreateAsync("Target");
        var source = await db.CreateAsync("Source", "`note://" + target.Summary.Id + "`\n\n[real](note://" + target.Summary.Id + "#heading)");
        var output = Path.Combine(root, "out");
        await new MarkdownTransfer(db).ExportAsync([source.Summary.Id, target.Summary.Id], output);
        var markdown = await File.ReadAllTextAsync(Path.Combine(output, "Source.md"));
        Assert.Contains("`note://" + target.Summary.Id + "`", markdown);
        Assert.Contains("[real](Target.md#heading)", markdown);
    }
}

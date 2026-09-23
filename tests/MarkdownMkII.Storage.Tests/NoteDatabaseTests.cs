using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage.Tests;
public sealed class NoteDatabaseTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mkii-db-tests-" + Guid.NewGuid().ToString("N"));
    private NoteDatabase Database => new(Path.Combine(root, "notes.db"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    [Fact]
    public async Task DiagnosticsReportCountsWithoutExposingContent()
    {
        var db = Database;
        var note = await db.CreateAsync("SECRET_TITLE", "SECRET_BODY");
        await db.SaveAsync(note.Summary.Id, "SECRET_BODY edited", note.Version, true);
        var trashed = await db.CreateAsync("Other");
        await db.SetStateAsync(trashed.Summary.Id, false, true);
        using (var input = new MemoryStream([1, 2, 3, 4]))
            await db.AddAttachmentAsync(input, "file.bin");

        var report = await db.DiagnosticsAsync();
        Assert.Equal(3, report.SchemaVersion);
        Assert.Equal("wal", report.JournalMode);
        Assert.Equal(1, report.Notes);
        Assert.Equal(1, report.TrashedNotes);
        Assert.Equal(1, report.Attachments);
        Assert.Equal(4, report.AttachmentBytes);
        Assert.True(report.Revisions >= 2);
        Assert.False(report.ProtectionConfigured);
        Assert.DoesNotContain("SECRET", report.ToString());
    }

    [Fact]
    public async Task InitializationCanRetryAfterAnIoFailure()
    {
        Directory.CreateDirectory(root);
        var blocked = Path.Combine(root, "blocked");
        await File.WriteAllTextAsync(blocked, "not a directory");
        var db = new NoteDatabase(Path.Combine(blocked, "notes.db"));
        await Assert.ThrowsAnyAsync<IOException>(() => db.InitializeAsync());
        File.Delete(blocked);
        await db.InitializeAsync();
        Assert.Equal(0, (await db.QueryAsync(new())).Total);
    }

    [Fact]
    public async Task EmptyArchiveDoesNotImportAnything()
    {
        Assert.Equal(0, (await Database.QueryAsync(new())).Total);
    }

    [Fact]
    public async Task SavesSurviveReopenAndRejectStaleWriters()
    {
        var db = Database;
        var note = await db.CreateAsync("Titolo", "Prima");
        var saved = await db.SaveAsync(note.Summary.Id, "Dopo", note.Version, true);
        Assert.Equal("Dopo", (await Database.GetAsync(note.Summary.Id))!.Markdown);
        await Assert.ThrowsAsync<NoteConflictException>(() => db.SaveAsync(note.Summary.Id, "Obsoleto", note.Version));
        Assert.Equal(2, saved.Version);
    }

    [Fact]
    public async Task SaveResultMatchesWhatIsStored()
    {
        var db = Database;
        var note = await db.CreateAsync("Titolo", "Prima");
        await db.UpdateMetadataAsync(note.Summary.Id, "Titolo", true, NoteColor.Blue, null, ["uno"]);
        var saved = await db.SaveAsync(note.Summary.Id, "Seconda versione", note.Version);
        var stored = (await db.GetAsync(note.Summary.Id))!;
        Assert.Equal(stored.Version, saved.Version);
        Assert.Equal(stored.Markdown, saved.Markdown);
        Assert.Equal(stored.Summary.Preview, saved.Summary.Preview);
        Assert.Equal(stored.Summary.Modified, saved.Summary.Modified);
        Assert.Equal(stored.Summary.Tags, saved.Summary.Tags);
        Assert.True(saved.Summary.Favorite);
        var unchanged = await db.SaveAsync(note.Summary.Id, "Seconda versione", saved.Version);
        Assert.Equal(saved.Version, unchanged.Version);
    }

    [Fact]
    public async Task ResetErasesNotesProtectionAndAttachmentsAndLeavesAUsableArchive()
    {
        var db = Database;
        await db.ConfigureProtectionAsync("correct horse battery staple");
        var plain = await db.CreateAsync("Plain", "text");
        await db.CreateProtectedAsync("Secret", "hidden");
        using (var data = new MemoryStream([4, 5, 6])) await db.AddAttachmentAsync(data, "a.png", noteId: plain.Summary.Id);

        await db.ResetAsync();

        Assert.False(db.IsUnlocked);
        Assert.False(db.IsProtectionConfigured);
        Assert.Equal(0, (await db.QueryAsync(new())).Total);
        Assert.Null(await db.FindAttachmentAsync("a.png"));
        Assert.Equal(0, (await Database.QueryAsync(new())).Total);
        var fresh = await db.CreateAsync("Again", "works");
        Assert.Equal("works", (await db.GetAsync(fresh.Summary.Id))!.Markdown);
    }

    [Fact]
    public async Task RelativeReferencesResolveToAttachmentsByName()
    {
        var db = Database;
        using var first = new MemoryStream([1, 2, 3]);
        var id = await db.AddAttachmentAsync(first, @"C:\photos\Shot 1.png");
        Assert.Equal(id, await db.FindAttachmentAsync("shot 1.PNG"));
        Assert.Equal(id, await db.FindAttachmentAsync("./img/Shot 1.png"));
        Assert.Null(await db.FindAttachmentAsync("missing.png"));
    }

    [Fact]
    public async Task MetadataRenamesAndDeletesPreserveNotes()
    {
        var db = Database;
        var note = await db.CreateAsync("Nota");
        var category = await db.AddCategoryAsync("  Lavoro  ");
        Assert.Equal(category, await db.AddCategoryAsync("lavoro"));
        await db.UpdateMetadataAsync(note.Summary.Id, "Nuovo titolo", true, NoteColor.Teal, category, ["#uno", " UNO ", "due"]);
        var loaded = (await db.GetAsync(note.Summary.Id))!;
        Assert.Equal(2, loaded.Summary.Tags.Count);
        Assert.True(loaded.Summary.Favorite);
        Assert.Equal(NoteColor.Teal, loaded.Summary.Color);
        await db.RenameLabelAsync(true, category, "Progetti");
        Assert.Equal("Progetti", (await db.GetAsync(note.Summary.Id))!.Summary.Category);
        await db.DeleteLabelAsync(true, category);
        Assert.Null((await db.GetAsync(note.Summary.Id))!.Summary.CategoryId);
        var tag = (await db.LabelsAsync(false)).First();
        await db.DeleteLabelAsync(false, tag.Id);
        Assert.Single((await db.GetAsync(note.Summary.Id))!.Summary.Tags);
    }

    [Fact]
    public async Task SearchPaginationAndCollectionsAreIndependentOfOpenDocuments()
    {
        var db = Database;
        for (var i = 0; i < 25; i++)
            await db.CreateAsync("Progetto " + i, "Caffè e attività");
        var first = await db.QueryAsync(new(Search: "caffe", Limit: 10));
        var second = await db.QueryAsync(new(Search: "caffe", Offset: 10, Limit: 10));
        Assert.Equal(25, first.Total);
        Assert.Empty(first.Items.Select(n => n.Id).Intersect(second.Items.Select(n => n.Id)));
        var id = first.Items[0].Id;
        await db.SetStateAsync(id, true, false);
        Assert.Equal(24, (await db.QueryAsync(new())).Total);
        Assert.Single((await db.QueryAsync(new(Collection: NoteCollection.Archive))).Items);
        await db.SetStateAsync(id, true, true);
        Assert.Single((await db.QueryAsync(new(Collection: NoteCollection.Trash))).Items);
        await db.DeletePermanentlyAsync(id);
        Assert.Null(await db.GetAsync(id));
    }

    [Fact]
    public async Task CheckpointsAreBoundedAndDoNotDuplicateUnchangedText()
    {
        var db = Database;
        var note = await db.CreateAsync("Cronologia");
        for (var i = 0; i < 35; i++)
            note = await db.SaveAsync(note.Summary.Id, i.ToString(), note.Version, true);
        await db.SaveAsync(note.Summary.Id, note.Markdown, note.Version, true);
        var revisions = await db.RevisionsAsync(note.Summary.Id);
        Assert.Equal(30, revisions.Count);
        Assert.Equal("34", revisions[0].Markdown);
    }

    [Fact]
    public async Task AttachmentsAreDeduplicatedAndBackupRestoresEverything()
    {
        var db = Database;
        var note = await db.CreateAsync("Con allegato");
        using var a = new MemoryStream([1, 2, 3, 4]);
        var id = await db.AddAttachmentAsync(a, "a.png");
        using var b = new MemoryStream([1, 2, 3, 4]);
        Assert.Equal(id, await db.AddAttachmentAsync(b, "b.png"));
        var backup = Path.Combine(root, "backup.db");
        await db.BackupAsync(backup);
        await db.SetStateAsync(note.Summary.Id, false, true);
        await db.DeletePermanentlyAsync(note.Summary.Id);
        await db.RestoreAsync(backup);
        Assert.NotNull(await db.GetAsync(note.Summary.Id));
        using var restored = new MemoryStream();
        await db.CopyAttachmentAsync(id, restored);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, restored.ToArray());
    }

    [Fact]
    public async Task InvalidRestoreLeavesCurrentArchiveIntact()
    {
        var db = Database;
        await db.CreateAsync("Keep");
        var invalid = Path.Combine(root, "invalid.db");
        await File.WriteAllTextAsync(invalid, "not sqlite");
        await Assert.ThrowsAnyAsync<Exception>(() => db.RestoreAsync(invalid));
        Assert.Equal(1, (await db.QueryAsync(new())).Total);
    }

    [Fact]
    public async Task LinkIdentitySurvivesTitleChanges()
    {
        var db = Database;
        var target = await db.CreateAsync("Target");
        var source = await db.CreateAsync("Source", "[link](note://" + target.Summary.Id + ")");
        await db.UpdateMetadataAsync(target.Summary.Id, "Renamed", false, NoteColor.None, null, []);
        Assert.Single(await db.ResolveAsync("note://" + target.Summary.Id));
        Assert.Single(await db.BacklinksAsync(target.Summary.Id, "Renamed"));
    }
}

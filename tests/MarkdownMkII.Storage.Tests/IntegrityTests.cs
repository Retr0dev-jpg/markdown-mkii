using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage.Tests;

public sealed class IntegrityTests : IDisposable
{
    private const string Password = "test passphrase for notes";
    private const string NewPassword = "a different test passphrase";
    private readonly string root = Path.Combine(Path.GetTempPath(), "mkii-integrity-tests-" + Guid.NewGuid().ToString("N"));
    private readonly MemoryAnchor anchor = new();
    private string PathFor(string name) => Path.Combine(root, name);
    private NoteDatabase Open(IIntegrityAnchor? store = null) => new(PathFor("notes.db")) { Anchor = store ?? anchor };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public async Task UntouchedArchiveUnlocksWithoutReportAcrossSavesAndCredentialChanges()
    {
        var db = Open();
        var recovery = await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "first");
        using (var input = new MemoryStream([1, 2, 3]))
            await db.AddAttachmentAsync(input, "file.bin", noteId: note.Summary.Id);
        note = await db.SaveAsync(note.Summary.Id, "second", note.Version, true);
        await db.ChangePasswordAsync(Password, NewPassword);
        await db.LockAsync();
        await db.UnlockAsync(NewPassword);
        Assert.Null(db.IntegrityReport);
        await db.LockAsync();
        await db.RecoverAsync(recovery, Password);
        Assert.Null(db.IntegrityReport);
        await db.UnprotectAsync(note.Summary.Id);
        await db.LockAsync();
        var reopened = Open();
        await reopened.UnlockAsync(Password);
        Assert.Null(reopened.IntegrityReport);
    }

    [Fact]
    public async Task RolledBackRecordIsReportedAndBlocksProtectedWritesUntilAccepted()
    {
        var db = Open();
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "first");
        var intact = await db.CreateProtectedAsync("Other", "untouched");
        var old = Columns(db, note.Summary.Id);
        note = await db.SaveAsync(note.Summary.Id, "second", note.Version);
        await db.LockAsync();

        // Dropping the upkeep trigger changes nothing: the app restores it and the digest is compared anyway.
        Raw(db, "DROP TRIGGER IntegrityNoteUpdate",
            ("UPDATE Notes SET Cipher=$cipher,Metadata=$metadata,Version=$version WHERE Id=$id",
                [("$cipher", old.Cipher), ("$metadata", old.Metadata), ("$version", old.Version), ("$id", note.Summary.Id)]));
        db = Open();
        await db.UnlockAsync(Password);

        var report = Assert.IsType<ArchiveIntegrityReport>(db.IntegrityReport);
        Assert.False(report.Unverifiable);
        Assert.False(report.RolledBack);
        Assert.Equal([note.Summary.Id], report.NoteIds);
        Assert.Equal("first", (await db.GetAsync(note.Summary.Id))!.Markdown);
        await Assert.ThrowsAsync<ArchiveIntegrityException>(() => db.SaveAsync(intact.Summary.Id, "edited", intact.Version));

        await db.AcceptIntegrityAsync();
        Assert.Null(db.IntegrityReport);
        await db.SaveAsync(intact.Summary.Id, "edited", intact.Version);
        await db.LockAsync();
        var reopened = Open();
        await reopened.UnlockAsync(Password);
        Assert.Null(reopened.IntegrityReport);
    }

    [Fact]
    public async Task OutsideChangesNeverBlockPlainNotes()
    {
        var db = Open();
        await db.ConfigureProtectionAsync(Password);
        var secret = await db.CreateProtectedAsync("Private", "body");
        var plain = await db.CreateAsync("Plain", "text");
        await db.LockAsync();
        Raw(db, "UPDATE Notes SET Metadata=zeroblob(64) WHERE Id='" + secret.Summary.Id + "'");
        plain = await db.SaveAsync(plain.Summary.Id, "saved while locked", plain.Version);
        await db.UnlockAsync(Password);
        Assert.Equal([secret.Summary.Id], db.IntegrityReport!.NoteIds);
        plain = await db.SaveAsync(plain.Summary.Id, "saved during the report", plain.Version);
        Assert.Equal("saved during the report", plain.Markdown);
    }

    [Fact]
    public async Task EditedDigestBreaksTheSealAndRemovedRevisionsAreAttributed()
    {
        var db = Open();
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "body");
        await db.LockAsync();
        Raw(db, "UPDATE Integrity SET Digest=zeroblob(32) WHERE Kind='n'");
        await db.UnlockAsync(Password);
        Assert.True(db.IntegrityReport!.Unverifiable);
        await db.AcceptIntegrityAsync();

        await db.LockAsync();
        Raw(db, "DROP TRIGGER IntegrityRevisionDelete", ("DELETE FROM Revisions WHERE NoteId=$id", [("$id", note.Summary.Id)]));
        await db.UnlockAsync(Password);
        Assert.Equal([note.Summary.Id], db.IntegrityReport!.NoteIds);
    }

    [Fact]
    public async Task StrippingTheIntegrityKeyIsDetected()
    {
        var db = Open();
        await db.ConfigureProtectionAsync(Password);
        await db.CreateProtectedAsync("Private", "body");
        await db.LockAsync();
        Raw(db, "UPDATE Protection SET IntegrityKey=NULL,IntegritySeal=NULL,IntegrityGeneration=0 WHERE Id=1");
        await db.UnlockAsync(Password);
        Assert.True(db.IntegrityReport!.Unverifiable);
    }

    [Fact]
    public async Task ReplacingTheWholeFileWithAnOlderCopyIsReportedOnlyWithTheAnchor()
    {
        var db = Open();
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "old");
        await db.BackupAsync(PathFor("older.db"));
        await db.SaveAsync(note.Summary.Id, "new", note.Version);
        await db.LockAsync();
        ReplaceArchive("older.db");

        var restored = Open();
        await restored.UnlockAsync(Password);
        var report = Assert.IsType<ArchiveIntegrityReport>(restored.IntegrityReport);
        Assert.True(report.RolledBack);
        Assert.Empty(report.NoteIds);

        await restored.LockAsync();
        var elsewhere = Open(new MemoryAnchor());
        await elsewhere.UnlockAsync(Password);
        Assert.Null(elsewhere.IntegrityReport);
    }

    [Fact]
    public async Task MissedAnchorWriteIsAcceptedBecauseTheFileIsAhead()
    {
        var db = Open();
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "body");
        anchor.Fail = true;
        await db.SaveAsync(note.Summary.Id, "edited", note.Version);
        anchor.Fail = false;
        await db.LockAsync();
        await db.UnlockAsync(Password);
        Assert.Null(db.IntegrityReport);
    }

    [Fact]
    public async Task RestoringABackupMakesItTheNewReference()
    {
        var db = Open();
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "old");
        await db.BackupAsync(PathFor("backup.db"));
        await db.SaveAsync(note.Summary.Id, "new", note.Version);
        await db.RestoreAsync(PathFor("backup.db"));
        await db.UnlockAsync(Password);
        Assert.Null(db.IntegrityReport);
        Assert.Equal("old", (await db.GetAsync(note.Summary.Id))!.Markdown);
    }

    [Fact]
    public async Task FactoryResetForgetsTheAnchor()
    {
        var db = Open();
        await db.ConfigureProtectionAsync(Password);
        await db.CreateProtectedAsync("Private", "body");
        Assert.NotEmpty(anchor.Values);
        await db.ResetAsync();
        Assert.Empty(anchor.Values);
    }

    private void ReplaceArchive(string source)
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            if (File.Exists(PathFor("notes.db" + suffix))) File.Delete(PathFor("notes.db" + suffix));
        File.Copy(PathFor(source), PathFor("notes.db"));
    }

    private static (byte[] Cipher, byte[] Metadata, long Version) Columns(NoteDatabase db, string id)
    {
        using var connection = Connect(db);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Cipher,Metadata,Version FROM Notes WHERE Id=$id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return ((byte[])reader[0], (byte[])reader[1], reader.GetInt64(2));
    }

    private static void Raw(NoteDatabase db, string sql, params (string Sql, (string Name, object Value)[] Parameters)[] more)
    {
        using var connection = Connect(db);
        foreach (var (text, parameters) in more.Prepend((sql, Array.Empty<(string, object)>())))
        {
            using var command = connection.CreateCommand();
            command.CommandText = text;
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            command.ExecuteNonQuery();
        }
    }

    private static SqliteConnection Connect(NoteDatabase db)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db.FilePath, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private sealed class MemoryAnchor : IIntegrityAnchor
    {
        public Dictionary<string, byte[]> Values { get; } = [];
        public bool Fail { get; set; }
        public byte[]? Load(string archiveId) => Values.TryGetValue(archiveId, out var value) ? value.ToArray() : null;
        public void Store(string archiveId, byte[] value)
        {
            if (Fail) throw new IOException("simulated anchor failure");
            Values[archiveId] = value.ToArray();
        }
        public void Forget(string archiveId) => Values.Remove(archiveId);
    }
}

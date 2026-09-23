using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage.Tests;

public sealed class NoteProtectionTests : IDisposable
{
    private const string Password = "test passphrase for notes";
    private const string NewPassword = "a different test passphrase";
    private readonly string root = Path.Combine(Path.GetTempPath(), "mkii-protection-tests-" + Guid.NewGuid().ToString("N"));
    private string PathFor(string name) => Path.Combine(root, name);
    private NoteDatabase Database => new(PathFor("notes.db"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Fact]
    public async Task CatalogConnectionsSharePrivateMetadataAndClearItOnLock()
    {
        var db = Database;
        Assert.Empty(await db.LabelsAsync(true));
        Assert.Empty(await db.LabelsAsync(false));
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateAsync("PrivateTitle", "PrivateBody");
        await db.ProtectAsync(note.Summary.Id);
        await db.SetProtectionSettingsAsync(true, 5);
        await db.SetPrivateCategoryAsync(note.Summary.Id, "PrivateCategory");
        var category = Assert.Single(await db.LabelsAsync(true));
        await db.UpdateMetadataAsync(note.Summary.Id, "PrivateTitle", true, NoteColor.Teal, category.Id, ["PrivateTag"]);
        Assert.Single((await db.QueryAsync(new(Search: "PrivateTag"))).Items);
        Assert.Single((await db.QueryAsync(new(Search: "PrivateCategory"))).Items);
        Assert.Empty((await db.QueryAsync(new(Search: "PrivateBody"))).Items);
        await db.LockAsync();
        var locked = Assert.Single((await db.QueryAsync(new())).Items);
        Assert.True(locked.IsLocked);
        Assert.Empty(locked.Title);
        Assert.Empty(locked.Tags);
        Assert.Null(locked.Category);
        Assert.False(locked.Favorite);
        Assert.Equal(NoteColor.None, locked.Color);
        Assert.Empty(await db.LabelsAsync(true));
        Assert.Empty(await db.LabelsAsync(false));
        Assert.Empty((await db.QueryAsync(new(Search: "PrivateTag"))).Items);
        await Assert.ThrowsAsync<NoteLockedException>(() => db.GetAsync(note.Summary.Id));
        await db.UnlockAsync(Password);
        Assert.Equal("PrivateBody", (await db.GetAsync(note.Summary.Id))!.Markdown);
        Assert.Single((await db.QueryAsync(new(Search: "PrivateTag"))).Items);
        await db.RenameLabelAsync(true, category.Id, "RenamedCategory");
        Assert.Equal("RenamedCategory", Assert.Single(await db.LabelsAsync(true)).Name);
        await db.DeleteLabelAsync(true, category.Id);
        Assert.Null((await db.GetAsync(note.Summary.Id))!.Summary.Category);
    }

    [Fact]
    public async Task PasswordRotationRecoveryAndDeviceWrappingRequireTheActualKey()
    {
        var db = Database;
        await Assert.ThrowsAsync<ArgumentException>(() => db.ConfigureProtectionAsync("too short"));
        var recovery = await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "secret");
        await db.LockAsync();
        await Assert.ThrowsAsync<ProtectionAuthenticationException>(() => db.UnlockAsync("wrong password"));
        Assert.False(db.IsUnlocked);
        await Assert.ThrowsAnyAsync<CryptographicException>(() => db.UnlockWithDeviceAsync(() => new byte[32]));
        Assert.False(db.IsUnlocked);
        await db.ChangePasswordAsync(Password, NewPassword);
        await Assert.ThrowsAsync<ProtectionAuthenticationException>(() => db.UnlockAsync(Password));
        var newRecovery = await db.RecoverAsync(recovery, Password);
        Assert.NotEqual(recovery, newRecovery);
        Assert.Equal("secret", (await db.GetAsync(note.Summary.Id))!.Markdown);
        await Assert.ThrowsAsync<ProtectionAuthenticationException>(() => db.RecoverAsync(recovery, NewPassword));
        var wrapped = await db.WrapForDeviceAsync(Password, key => key.ToArray());
        try
        {
            await db.LockAsync();
            await db.UnlockWithDeviceAsync(() => wrapped.ToArray());
            Assert.Equal("secret", (await db.GetAsync(note.Summary.Id))!.Markdown);
        }
        finally { CryptographicOperations.ZeroMemory(wrapped); }
    }

    [Fact]
    public async Task CredentialChangesRotateTheMasterKeySoOldSecretsOpenNothingInTheMainFile()
    {
        var db = Database;
        var recovery = await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "secret");
        var oldDevice = await db.WrapForDeviceAsync(Password, key => key.ToArray());
        try
        {
            await db.ChangePasswordAsync(Password, NewPassword);
            Assert.True(db.IsUnlocked);
            Assert.Equal("secret", (await db.GetAsync(note.Summary.Id))!.Markdown);

            // A copy of the main file alone (no WAL) must not accept the old password.
            SqliteConnection.ClearAllPools();
            var copy = PathFor("copy.db");
            File.Copy(PathFor("notes.db"), copy);
            var stale = new NoteDatabase(copy);
            await Assert.ThrowsAsync<ProtectionAuthenticationException>(() => stale.UnlockAsync(Password));
            await stale.UnlockAsync(NewPassword);
            Assert.Equal("secret", (await stale.GetAsync(note.Summary.Id))!.Markdown);

            // The previous master key (for example a Windows Hello wrapper) no longer verifies.
            await db.LockAsync();
            await Assert.ThrowsAnyAsync<CryptographicException>(() => db.UnlockWithDeviceAsync(() => oldDevice.ToArray()));

            // Recovery survives the password change because its secret was rewrapped.
            var renewed = await db.RecoverAsync(recovery, Password);
            Assert.Equal("secret", (await db.GetAsync(note.Summary.Id))!.Markdown);
            var third = await db.RenewRecoveryCodeAsync(Password);
            await db.LockAsync();
            await Assert.ThrowsAsync<ProtectionAuthenticationException>(() => db.RecoverAsync(renewed, NewPassword));
            await db.RecoverAsync(third, NewPassword);
            Assert.Equal("secret", (await db.GetAsync(note.Summary.Id))!.Markdown);
        }
        finally { CryptographicOperations.ZeroMemory(oldDevice); }
    }

    [Fact]
    public async Task OneDamagedRecordDoesNotLockTheUserOutOfTheOthers()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var damaged = await db.CreateProtectedAsync("Damaged", "lost");
        var intact = await db.CreateProtectedAsync("Intact", "still here");
        await db.LockAsync();
        Execute(db, $"UPDATE Notes SET Metadata=zeroblob(64) WHERE Id='{damaged.Summary.Id}'");
        await db.UnlockAsync(Password);
        Assert.True(db.IsUnlocked);
        Assert.Contains(damaged.Summary.Id, db.DamagedNoteIds);
        Assert.Equal("still here", (await db.GetAsync(intact.Summary.Id))!.Markdown);
    }

    [Fact]
    public async Task TamperedFlagsAndRolledBackBodiesAreRejected()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "first");
        var old = (byte[])Scalar(db, $"SELECT Cipher FROM Notes WHERE Id='{note.Summary.Id}'")!;
        var saved = await db.SaveAsync(note.Summary.Id, "second", note.Version);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = PathFor("notes.db"), Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Notes SET Cipher=$old WHERE Id=$id";
            command.Parameters.AddWithValue("$old", old);
            command.Parameters.AddWithValue("$id", note.Summary.Id);
            command.ExecuteNonQuery();
        }
        await Assert.ThrowsAnyAsync<CryptographicException>(() => db.GetAsync(note.Summary.Id));

        var other = await db.CreateProtectedAsync("Other", "text");
        Execute(db, $"UPDATE Notes SET Protected=0 WHERE Id='{other.Summary.Id}'");
        await Assert.ThrowsAsync<InvalidDataException>(() => db.GetAsync(other.Summary.Id));
        Assert.Equal(2, saved.Version);
    }

    [Fact]
    public async Task CiphertextSizeHidesTheExactNoteLength()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var shortNote = await db.CreateProtectedAsync("A", "short");
        var longNote = await db.CreateProtectedAsync("B", new string('x', 200));
        var shortLength = Convert.ToInt64(Scalar(db, $"SELECT length(Cipher) FROM Notes WHERE Id='{shortNote.Summary.Id}'"));
        var longLength = Convert.ToInt64(Scalar(db, $"SELECT length(Cipher) FROM Notes WHERE Id='{longNote.Summary.Id}'"));
        Assert.Equal(shortLength, longLength);
        Assert.Equal(new string('x', 200), (await db.GetAsync(longNote.Summary.Id))!.Markdown);
    }

    [Fact]
    public void PasswordPolicyRejectsRepeatedCharacters()
    {
        Assert.False(PasswordPolicy.IsAcceptable("aaaaaaaaaaaa"));
        Assert.False(PasswordPolicy.IsAcceptable("abababababab"));
        Assert.False(PasswordPolicy.IsAcceptable("short pass"));
        Assert.True(PasswordPolicy.IsAcceptable(Password));
    }

    [Fact]
    public async Task ArchivesWithoutRecoveryEscrowStillChangePasswordAndKeepRecovery()
    {
        var db = Database;
        var recovery = await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Private", "secret");
        Execute(db, "UPDATE Protection SET RecoverySecret=NULL WHERE Id=1");
        await db.ChangePasswordAsync(Password, NewPassword);
        await db.LockAsync();
        await Assert.ThrowsAsync<ProtectionAuthenticationException>(() => db.UnlockAsync(Password));
        await db.RecoverAsync(recovery, Password);
        Assert.Equal("secret", (await db.GetAsync(note.Summary.Id))!.Markdown);
    }

    [Fact]
    public async Task ProtectionScrubsPlaintextHistoryAssetsAndHiddenMetadata()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var assetBytes = Encoding.UTF8.GetBytes("ASSET_UNIQUE_SECRET_MARKER_780029");
        using var input = new MemoryStream(assetBytes);
        var asset = await db.AddAttachmentAsync(input, "SECRET_ASSET_NAME.bin");
        var note = await db.CreateAsync("SECRET_TITLE_MARKER", "SECRET_OLD_REVISION ![asset](attachment://" + asset + ")");
        note = await db.SaveAsync(note.Summary.Id, "SECRET_BODY_MARKER [secret target](note://SECRET_LINK_TARGET)", note.Version, true);
        var category = await db.AddCategoryAsync("SECRET_CATEGORY_MARKER");
        await db.UpdateMetadataAsync(note.Summary.Id, note.Summary.Title, true, NoteColor.Red, category, ["SECRET_TAG_MARKER"]);
        await db.ProtectAsync(note.Summary.Id);
        await db.SetProtectionSettingsAsync(true, 5);
        Assert.False((await db.ProtectionSettingsAsync()).MaintenancePending);
        Assert.Equal(note.Markdown, (await db.GetAsync(note.Summary.Id))!.Markdown);
        Assert.Equal(2, (await db.RevisionsAsync(note.Summary.Id)).Count);
        await Assert.ThrowsAsync<FileNotFoundException>(() => db.CopyAttachmentAsync(asset, new MemoryStream()));
        await db.LockAsync();
        foreach (var file in Directory.EnumerateFiles(root))
        {
            var bytes = await File.ReadAllBytesAsync(file);
            foreach (var marker in new[] { "SECRET_BODY_MARKER", "SECRET_OLD_REVISION", "SECRET_TITLE_MARKER", "SECRET_CATEGORY_MARKER", "SECRET_TAG_MARKER", "SECRET_LINK_TARGET", "SECRET_ASSET_NAME", "ASSET_UNIQUE_SECRET_MARKER_780029" })
            {
                Assert.DoesNotContain(marker, Encoding.UTF8.GetString(bytes));
                Assert.DoesNotContain(marker, Encoding.Unicode.GetString(bytes));
            }
        }
        await Assert.ThrowsAsync<NoteLockedException>(() => db.RevisionsAsync(note.Summary.Id));
    }

    [Fact]
    public async Task BackupKeepsProtectionAndPendingDraftCanBeRecoveredAfterUnlock()
    {
        var db = Database;
        var recovery = await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Backed up", "saved");
        var draft = await db.SealDraftAsync(note.Summary.Id, "unsaved", note.Version);
        await db.BackupAsync(PathFor("backup.db"));
        await db.LockAsync();
        await Assert.ThrowsAsync<NoteLockedException>(() => db.UnsealDraftAsync(draft));
        await db.UnlockAsync(Password);
        Assert.Equal("unsaved", await db.UnsealDraftAsync(draft));
        await db.SaveAsync(note.Summary.Id, "later", note.Version, true);
        await db.RestoreAsync(PathFor("backup.db"));
        Assert.False(db.IsUnlocked);
        await db.RecoverAsync(recovery, NewPassword);
        Assert.Equal("saved", (await db.GetAsync(note.Summary.Id))!.Markdown);
        var reopened = Database;
        await Assert.ThrowsAsync<NoteLockedException>(() => reopened.GetAsync(note.Summary.Id));
        await reopened.UnlockAsync(NewPassword);
        Assert.Equal("saved", (await reopened.GetAsync(note.Summary.Id))!.Markdown);
    }

    [Fact]
    public async Task DuplicateHasIndependentKeyAndPrivateAttachmentsAndUnprotectRestoresPlaintext()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var source = await db.CreateProtectedAsync("Original", "body");
        using var input = new MemoryStream([1, 2, 3, 4, 5]);
        var asset = await db.AddAttachmentAsync(input, "sample.bin", noteId: source.Summary.Id);
        source = await db.SaveAsync(source.Summary.Id, "[file](attachment://" + asset + ")", source.Version, true);
        var copy = await db.DuplicateAsync(source.Summary.Id, "Copy");
        Assert.True(copy.Summary.IsProtected);
        Assert.DoesNotContain(asset, copy.Markdown);
        await Assert.ThrowsAsync<NoteLockedException>(() => db.CopyAttachmentAsync(asset, new MemoryStream(), noteId: copy.Summary.Id));
        using (var connection = new SqliteConnection("Data Source=" + db.FilePath))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT hex(WrappedKey) FROM Notes WHERE Protected=1";
            using var reader = command.ExecuteReader();
            var keys = new HashSet<string>();
            while (reader.Read()) keys.Add(reader.GetString(0));
            Assert.Equal(2, keys.Count);
        }
        await db.UnprotectAsync(copy.Summary.Id);
        await db.LockAsync();
        var plain = (await db.GetAsync(copy.Summary.Id))!;
        Assert.False(plain.Summary.IsProtected);
        Assert.Contains("attachment://", plain.Markdown);
        await Assert.ThrowsAsync<NoteLockedException>(() => db.GetAsync(source.Summary.Id));
    }

    [Fact]
    public async Task FailedSaveCanBeSealedLockedAndRetriedWithoutLosingText()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateProtectedAsync("Failure", "persisted");
        Execute(db, "CREATE TRIGGER FailSave BEFORE UPDATE OF Cipher ON Notes BEGIN SELECT RAISE(FAIL,'simulated write failure'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => db.SaveAsync(note.Summary.Id, "latest edit", note.Version));
        var draft = await db.SealDraftAsync(note.Summary.Id, "latest edit", note.Version);
        await db.LockAsync();
        Execute(db, "DROP TRIGGER FailSave;");
        await db.UnlockAsync(Password);
        var saved = await db.SaveAsync(draft.NoteId, await db.UnsealDraftAsync(draft), draft.Version, true);
        Assert.Equal("latest edit", saved.Markdown);
        Assert.Contains(await db.RevisionsAsync(draft.NoteId), revision => revision.Markdown == "persisted");
    }

    [Fact]
    public async Task InterruptedCleanupRemainsPendingAndResumesOnReopen()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateAsync("Cleanup", "cleanup secret");
        Execute(db, "CREATE TRIGGER FailMaintenance BEFORE UPDATE OF Maintenance ON Protection WHEN new.Maintenance=0 BEGIN SELECT RAISE(FAIL,'simulated cleanup failure'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => db.ProtectAsync(note.Summary.Id));
        Assert.True((await db.ProtectionSettingsAsync()).MaintenancePending);
        Assert.True((await db.SummaryAsync(note.Summary.Id))!.IsProtected);
        Execute(db, "DROP TRIGGER FailMaintenance;");
        var reopened = Database;
        await reopened.InitializeAsync();
        Assert.False((await reopened.ProtectionSettingsAsync()).MaintenancePending);
        await reopened.UnlockAsync(Password);
        Assert.Equal("cleanup secret", (await reopened.GetAsync(note.Summary.Id))!.Markdown);
    }

    [Fact]
    public async Task AuthenticatedRecordsRejectTamperingAndCrossNoteSubstitution()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var first = await db.CreateProtectedAsync("First", "same contents");
        var second = await db.CreateProtectedAsync("Second", "same contents");
        var before = (byte[])Scalar(db, "SELECT Cipher FROM Notes WHERE Id='" + first.Summary.Id + "'")!;
        first = await db.SaveAsync(first.Summary.Id, "new contents", first.Version);
        first = await db.SaveAsync(first.Summary.Id, "same contents", first.Version);
        var after = (byte[])Scalar(db, "SELECT Cipher FROM Notes WHERE Id='" + first.Summary.Id + "'")!;
        Assert.False(before.AsSpan().SequenceEqual(after));
        Execute(db, "UPDATE Notes SET Cipher=(SELECT Cipher FROM Notes WHERE Id='" + first.Summary.Id + "') WHERE Id='" + second.Summary.Id + "'");
        await Assert.ThrowsAnyAsync<CryptographicException>(() => db.GetAsync(second.Summary.Id));
        var draft = await db.SealDraftAsync(first.Summary.Id, "pending", first.Version);
        draft.Data[^1] ^= 1;
        await Assert.ThrowsAnyAsync<CryptographicException>(() => db.UnsealDraftAsync(draft));
    }

    [Fact]
    public async Task PublicMetadataStaysSearchableButProtectedBodyNeverDoes()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateAsync("VisibleTitle", "HiddenBody");
        var category = await db.AddCategoryAsync("VisibleCategory");
        await db.UpdateMetadataAsync(note.Summary.Id, note.Summary.Title, true, NoteColor.Pink, category, ["VisibleTag"]);
        await db.ProtectAsync(note.Summary.Id);
        await db.LockAsync();
        foreach (var term in new[] { "VisibleTitle", "VisibleCategory", "VisibleTag" })
            Assert.Single((await db.QueryAsync(new(Search: term))).Items);
        Assert.Empty((await db.QueryAsync(new(Search: "HiddenBody"))).Items);
        await db.UnlockAsync(Password);
        await db.SetProtectionSettingsAsync(true, 1);
        await db.SetProtectionSettingsAsync(false, 30);
        await db.LockAsync();
        Assert.Single((await db.QueryAsync(new(Search: "VisibleTag"))).Items);
        Assert.Equal(30, (await db.ProtectionSettingsAsync()).IdleMinutes);
    }

    [Fact]
    public async Task ExportRequiresUnlockAndPreservesAttachmentsAndInternalLinks()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var first = await db.CreateProtectedAsync("First", "body");
        var second = await db.CreateProtectedAsync("Second", "target");
        using var input = new MemoryStream([8, 7, 6, 5]);
        var asset = await db.AddAttachmentAsync(input, "image.png", noteId: first.Summary.Id);
        first = await db.SaveAsync(first.Summary.Id, $"![image](attachment://{asset}) [target](note://{second.Summary.Id})", first.Version);
        var transfer = new MarkdownTransfer(db);
        await db.LockAsync();
        await Assert.ThrowsAsync<NoteLockedException>(() => transfer.ExportAsync([first.Summary.Id], PathFor("locked-export")));
        await db.UnlockAsync(Password);
        var folder = PathFor("export");
        await transfer.ExportAsync([first.Summary.Id, second.Summary.Id], folder);
        var exported = await File.ReadAllTextAsync(Path.Combine(folder, "First.md"));
        Assert.Contains("Second.md", exported);
        Assert.DoesNotContain("attachment://", exported);
        Assert.Equal(new byte[] { 8, 7, 6, 5 }, await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(Path.Combine(folder, "attachments")))));
    }

    private static object? Scalar(NoteDatabase db, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db.FilePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(NoteDatabase db, string sql) => Scalar(db, sql);

    [Fact]
    public async Task SchemaOneArchiveAndBackupMigrateWithoutProtectingExistingNotes()
    {
        Directory.CreateDirectory(root);
        var db = Database;
        // A schema-1 fixture has no protection columns, tables, or session catalog.
        Execute(db, """
            CREATE TABLE Categories(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Normalized TEXT NOT NULL UNIQUE);
            CREATE TABLE Tags(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Normalized TEXT NOT NULL UNIQUE);
            CREATE TABLE Notes(Id TEXT PRIMARY KEY,Title TEXT NOT NULL,Markdown TEXT NOT NULL,Preview TEXT NOT NULL,
                Created INTEGER NOT NULL,Modified INTEGER NOT NULL,Version INTEGER NOT NULL DEFAULT 1,
                Favorite INTEGER NOT NULL DEFAULT 0,Color INTEGER NOT NULL DEFAULT 0,
                CategoryId TEXT REFERENCES Categories(Id) ON DELETE SET NULL,Archived INTEGER NOT NULL DEFAULT 0,Trashed INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE NoteTags(NoteId TEXT REFERENCES Notes(Id) ON DELETE CASCADE,TagId TEXT REFERENCES Tags(Id) ON DELETE CASCADE,PRIMARY KEY(NoteId,TagId));
            CREATE TABLE Revisions(Id INTEGER PRIMARY KEY,NoteId TEXT NOT NULL REFERENCES Notes(Id) ON DELETE CASCADE,Markdown TEXT NOT NULL,Created INTEGER NOT NULL);
            CREATE TABLE Attachments(Id TEXT NOT NULL UNIQUE,Name TEXT NOT NULL,Data BLOB NOT NULL);
            CREATE TABLE NoteLinks(NoteId TEXT NOT NULL REFERENCES Notes(Id) ON DELETE CASCADE,Target TEXT NOT NULL,Label TEXT NOT NULL,Line INTEGER NOT NULL);
            CREATE TABLE ImportSources(Source TEXT NOT NULL,Hash TEXT NOT NULL,NoteId TEXT REFERENCES Notes(Id) ON DELETE CASCADE,PRIMARY KEY(Source,Hash));
            CREATE VIRTUAL TABLE NoteSearch USING fts5(Title,Markdown,content='Notes',content_rowid='rowid',tokenize='unicode61 remove_diacritics 2');
            CREATE TRIGGER SearchInsert AFTER INSERT ON Notes BEGIN INSERT INTO NoteSearch(rowid,Title,Markdown) VALUES(new.rowid,new.Title,new.Markdown); END;
            CREATE TRIGGER SearchDelete AFTER DELETE ON Notes BEGIN INSERT INTO NoteSearch(NoteSearch,rowid,Title,Markdown) VALUES('delete',old.rowid,old.Title,old.Markdown); END;
            CREATE TRIGGER SearchUpdate AFTER UPDATE OF Title,Markdown ON Notes BEGIN
                INSERT INTO NoteSearch(NoteSearch,rowid,Title,Markdown) VALUES('delete',old.rowid,old.Title,old.Markdown);
                INSERT INTO NoteSearch(rowid,Title,Markdown) VALUES(new.rowid,new.Title,new.Markdown); END;
            INSERT INTO Notes(Id,Title,Markdown,Preview,Created,Modified) VALUES('legacy','Old note','Original text','Original',1,1);
            PRAGMA user_version=1;
            """);
        File.Copy(db.FilePath, PathFor("legacy-backup.db"));
        await db.InitializeAsync();
        Assert.Equal(3L, Scalar(db, "PRAGMA user_version"));
        Assert.False((await db.GetAsync("legacy"))!.Summary.IsProtected);
        Assert.Single((await db.QueryAsync(new(Search: "Original"))).Items);
        await db.ConfigureProtectionAsync(Password);
        await db.ProtectAsync("legacy");
        await db.RestoreAsync(PathFor("legacy-backup.db"));
        Assert.False(db.IsUnlocked);
        Assert.False((await db.ProtectionSettingsAsync()).Configured);
        Assert.Equal("Original text", (await db.GetAsync("legacy"))!.Markdown);
        Assert.Empty(await db.LabelsAsync(true));
    }
}

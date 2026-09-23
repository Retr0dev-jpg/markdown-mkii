using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage;

public sealed partial class NoteDatabase : INoteProtection
{
    private volatile byte[]? masterKey;
    private readonly Dictionary<string, byte[]> noteKeys = new(StringComparer.Ordinal);
    private string archiveId = "";
    private bool hideDetails;
    private long sessionVersion;
    private readonly string memoryName = "mkii-security-" + Guid.NewGuid().ToString("N");
    // Keep the anchor connection and every ATTACH on exactly the same URI.
    private string MemoryUri => "file:" + memoryName + "?mode=memory&cache=shared";
    private SqliteConnection? memory;
    public bool IsUnlocked => masterKey is not null;
    public bool IsProtectionConfigured { get; private set; }
    public long SessionVersion => Interlocked.Read(ref sessionVersion);
    public event EventHandler? ProtectionChanged;
    private sealed record PrivateMetadata(string Title, bool Favorite, NoteColor Color, string? CategoryId, string? Category, string[] Tags, NoteLink[]? Links = null, Dictionary<string, string>? AttachmentAliases = null);
    private string Context(string id, string kind) => "MarkdownMkII:1:" + archiveId + ":" + id + ":" + kind;
    // Binding the row version rejects an older body blob copied back over the current one.
    private string BodyContext(string id, long version) => Context(id, "body:" + version);
    private string DecryptBody(byte[] key, byte[] cipher, string id, long version)
    {
        try { return NoteCipher.DecryptText(key, cipher, BodyContext(id, version)); }
        catch (CryptographicException) { return NoteCipher.DecryptText(key, cipher, Context(id, "body")); }
    }
    private byte[] Master => masterKey ?? throw new NoteLockedException();

    private void MigrateProtection(SqliteConnection db)
    {
        if (Convert.ToInt32(Scalar(db, "PRAGMA user_version")) == 1)
        {
            using var tx = db.BeginTransaction();
            Execute(db, """
                ALTER TABLE Notes ADD COLUMN Protected INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE Notes ADD COLUMN Cipher BLOB;
                ALTER TABLE Notes ADD COLUMN Metadata BLOB;
                ALTER TABLE Notes ADD COLUMN WrappedKey BLOB;
                ALTER TABLE Revisions ADD COLUMN Cipher BLOB;
                ALTER TABLE Attachments ADD COLUMN OwnerId TEXT REFERENCES Notes(Id) ON DELETE CASCADE;
                ALTER TABLE Attachments ADD COLUMN CipherName BLOB;
                CREATE INDEX AttachmentOwner ON Attachments(OwnerId);
                CREATE TABLE Protection(Id INTEGER PRIMARY KEY CHECK(Id=1), ArchiveId TEXT NOT NULL,
                    Salt BLOB, Iterations INTEGER, PasswordKey BLOB, RecoveryKey BLOB, Verifier BLOB,
                    HideDetails INTEGER NOT NULL DEFAULT 0, IdleMinutes INTEGER NOT NULL DEFAULT 5,
                    Maintenance INTEGER NOT NULL DEFAULT 0);
                INSERT INTO Protection(Id,ArchiveId) VALUES(1,lower(hex(randomblob(16))));
                CREATE VIRTUAL TABLE PublicMetadataSearch USING fts5(Title,Tags,Category,tokenize='unicode61 remove_diacritics 2');
                INSERT INTO PublicMetadataSearch(PublicMetadataSearch,rank) VALUES('secure-delete',1);
                CREATE VIEW PublicSearchValues AS SELECT n.rowid AS rowid,n.Id,n.Title,
                    coalesce((SELECT group_concat(t.Name,' ') FROM NoteTags nt JOIN Tags t ON t.Id=nt.TagId WHERE nt.NoteId=n.Id),'') AS Tags,
                    coalesce(c.Name,'') AS Category FROM Notes n LEFT JOIN Categories c ON c.Id=n.CategoryId;
                INSERT INTO PublicMetadataSearch(rowid,Title,Tags,Category) SELECT rowid,Title,Tags,Category FROM PublicSearchValues;
                CREATE TRIGGER PublicSearchInsert AFTER INSERT ON Notes BEGIN
                    INSERT INTO PublicMetadataSearch(rowid,Title,Tags,Category) SELECT rowid,Title,Tags,Category FROM PublicSearchValues WHERE Id=new.Id; END;
                CREATE TRIGGER PublicSearchDelete AFTER DELETE ON Notes BEGIN
                    DELETE FROM PublicMetadataSearch WHERE rowid=old.rowid; END;
                CREATE TRIGGER PublicSearchUpdate AFTER UPDATE OF Title,CategoryId ON Notes BEGIN
                    DELETE FROM PublicMetadataSearch WHERE rowid=new.rowid;
                    INSERT INTO PublicMetadataSearch(rowid,Title,Tags,Category) SELECT rowid,Title,Tags,Category FROM PublicSearchValues WHERE Id=new.Id; END;
                CREATE TRIGGER PublicSearchTagInsert AFTER INSERT ON NoteTags BEGIN
                    DELETE FROM PublicMetadataSearch WHERE rowid=(SELECT rowid FROM Notes WHERE Id=new.NoteId);
                    INSERT INTO PublicMetadataSearch(rowid,Title,Tags,Category) SELECT rowid,Title,Tags,Category FROM PublicSearchValues WHERE Id=new.NoteId; END;
                CREATE TRIGGER PublicSearchTagDelete AFTER DELETE ON NoteTags BEGIN
                    DELETE FROM PublicMetadataSearch WHERE rowid=(SELECT rowid FROM Notes WHERE Id=old.NoteId);
                    INSERT INTO PublicMetadataSearch(rowid,Title,Tags,Category) SELECT rowid,Title,Tags,Category FROM PublicSearchValues WHERE Id=old.NoteId; END;
                CREATE TRIGGER PublicSearchTagRename AFTER UPDATE OF Name ON Tags BEGIN
                    DELETE FROM PublicMetadataSearch WHERE rowid IN(SELECT n.rowid FROM Notes n JOIN NoteTags nt ON nt.NoteId=n.Id WHERE nt.TagId=new.Id);
                    INSERT INTO PublicMetadataSearch(rowid,Title,Tags,Category) SELECT rowid,Title,Tags,Category FROM PublicSearchValues WHERE Id IN(SELECT NoteId FROM NoteTags WHERE TagId=new.Id); END;
                CREATE TRIGGER PublicSearchCategoryRename AFTER UPDATE OF Name ON Categories BEGIN
                    DELETE FROM PublicMetadataSearch WHERE rowid IN(SELECT rowid FROM Notes WHERE CategoryId=new.Id);
                    INSERT INTO PublicMetadataSearch(rowid,Title,Tags,Category) SELECT rowid,Title,Tags,Category FROM PublicSearchValues WHERE Id IN(SELECT Id FROM Notes WHERE CategoryId=new.Id); END;
                INSERT INTO NoteSearch(NoteSearch,rank) VALUES('secure-delete',1);
                PRAGMA user_version=2;
                """);
            tx.Commit();
        }
        if (Convert.ToInt32(Scalar(db, "SELECT count(*) FROM pragma_table_info('Protection') WHERE name='RecoverySecret'")) == 0)
            Execute(db, "ALTER TABLE Protection ADD COLUMN RecoverySecret BLOB;");
        MigrateIntegrity(db);
        IsProtectionConfigured = Scalar(db, "SELECT PasswordKey FROM Protection WHERE Id=1") is byte[];
        archiveId = (string)Scalar(db, "SELECT ArchiveId FROM Protection WHERE Id=1")!;
        hideDetails = Convert.ToBoolean(Scalar(db, "SELECT HideDetails FROM Protection WHERE Id=1"));
        memory ??= new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = MemoryUri, Pooling = false }.ToString());
        if (memory.State != System.Data.ConnectionState.Open) memory.Open();
        Execute(memory, """
            PRAGMA secure_delete=ON;
            CREATE TABLE IF NOT EXISTS Metadata(Id TEXT PRIMARY KEY,Title TEXT,Favorite INTEGER,Color INTEGER,CategoryId TEXT,Category TEXT,Tags TEXT);
            CREATE TABLE IF NOT EXISTS Categories(Id TEXT PRIMARY KEY,Name TEXT);
            CREATE TABLE IF NOT EXISTS Tags(NoteId TEXT,Name TEXT,Normalized TEXT);
            CREATE INDEX IF NOT EXISTS TagsNote ON Tags(NoteId);
            CREATE INDEX IF NOT EXISTS TagsName ON Tags(Normalized);
            CREATE INDEX IF NOT EXISTS MetadataCategory ON Metadata(CategoryId);
            CREATE TABLE IF NOT EXISTS Links(NoteId TEXT,Target TEXT,Label TEXT,Line INTEGER);
            CREATE INDEX IF NOT EXISTS LinksTarget ON Links(Target);
            CREATE INDEX IF NOT EXISTS LinksNote ON Links(NoteId);
            CREATE VIRTUAL TABLE IF NOT EXISTS Search USING fts5(Id UNINDEXED,Title,Tags,Category,tokenize='unicode61 remove_diacritics 2');
            INSERT INTO Search(Search,rank) VALUES('secure-delete',1);
            """);
        if (Convert.ToBoolean(Scalar(db, "SELECT Maintenance FROM Protection WHERE Id=1"))) Scrub(db);
    }

    private void AttachCatalog(SqliteConnection db)
    {
        Execute(db, "ATTACH DATABASE $uri AS session", ("$uri", MemoryUri));
        Execute(db, """
            CREATE TEMP VIEW Catalog AS SELECT n.rowid AS rowid,n.Id,
                coalesce(m.Title,n.Title) AS Title,n.Markdown,n.Preview,n.Created,n.Modified,n.Version,
                coalesce(m.Favorite,n.Favorite) AS Favorite,coalesce(m.Color,n.Color) AS Color,
                CASE WHEN m.Id IS NOT NULL THEN m.CategoryId ELSE n.CategoryId END AS CategoryId,
                n.Archived,n.Trashed,n.Protected,n.Cipher,n.Metadata,n.WrappedKey
                FROM main.Notes n LEFT JOIN session.Metadata m ON m.Id=n.Id;
            CREATE TEMP VIEW CatalogCategories AS SELECT Id,Name FROM main.Categories
                UNION SELECT Id,Name FROM session.Categories;
            CREATE TEMP VIEW CatalogTags AS SELECT n.Id AS NoteId,t.Name AS Name, t.Normalized AS Normalized
                FROM main.NoteTags nt JOIN main.Tags t ON t.Id=nt.TagId JOIN main.Notes n ON n.Id=nt.NoteId
                WHERE NOT EXISTS(SELECT 1 FROM session.Metadata m WHERE m.Id=n.Id)
                UNION ALL SELECT NoteId,Name,Normalized FROM session.Tags;
            """);
    }

    public Task<ProtectionSettings> ProtectionSettingsAsync() => Read(db => new ProtectionSettings(
        Scalar(db, "SELECT PasswordKey FROM Protection WHERE Id=1") is byte[], hideDetails,
        Convert.ToInt32(Scalar(db, "SELECT IdleMinutes FROM Protection WHERE Id=1")),
        Convert.ToBoolean(Scalar(db, "SELECT Maintenance FROM Protection WHERE Id=1"))));

    public async Task<string> ConfigureProtectionAsync(string password)
    {
        NoteCipher.ValidatePassword(password);
        var code = await Write(db =>
        {
            if (Scalar(db, "SELECT PasswordKey FROM Protection WHERE Id=1") is byte[])
                throw new InvalidOperationException("Protection is already configured.");
            var key = SecretMemory.Random(32);
            try
            {
                StorePassword(db, password, key);
                return StoreRecovery(db, key);
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        });
        await UnlockAsync(password);
        return code;
    }

    private void StorePassword(SqliteConnection db, string password, byte[] key)
    {
        IsProtectionConfigured = true;
        var salt = RandomNumberGenerator.GetBytes(32);
        var derived = NoteCipher.PasswordKey(password, salt, NoteCipher.Iterations);
        try
        {
            Execute(db, "UPDATE Protection SET Salt=$salt,Iterations=$iterations,PasswordKey=$key,Verifier=$verifier WHERE Id=1",
                ("$verifier", NoteCipher.EncryptText(key, archiveId, VerifierContext(db))), ("$salt", salt), ("$iterations", NoteCipher.Iterations), ("$key", NoteCipher.Encrypt(derived, key, Context("vault", "password"))));
        }
        finally { CryptographicOperations.ZeroMemory(derived); }
    }

    private string StoreRecovery(SqliteConnection db, byte[] key)
    {
        var recovery = SecretMemory.Random(32);
        try
        {
            StoreRecoveryWrappers(db, key, recovery);
            return NoteCipher.RecoveryCode(recovery);
        }
        finally { CryptographicOperations.ZeroMemory(recovery); }
    }

    // The recovery secret is also kept under the master key, so a password change can rotate the master
    // key and still rewrap recovery. It grants nothing beyond the master key that decrypts it.
    private void StoreRecoveryWrappers(SqliteConnection db, byte[] master, byte[] recovery)
        => Execute(db, "UPDATE Protection SET RecoveryKey=$key,RecoverySecret=$secret WHERE Id=1",
            ("$key", NoteCipher.Encrypt(recovery, master, Context("vault", "recovery"))),
            ("$secret", NoteCipher.Encrypt(master, recovery, Context("vault", "recovery-secret"))));

    private byte[]? RecoverySecret(SqliteConnection db, byte[] master)
        => Scalar(db, "SELECT RecoverySecret FROM Protection WHERE Id=1") is byte[] escrow
            ? TryUnwrap(master, escrow, Context("vault", "recovery-secret"))
            : null;

    /// <summary>
    /// Wraps every note key and the integrity key under a fresh master key and returns it. The caller
    /// must rewrite the password and recovery wrappers with the new key in the same transaction; old
    /// wrappers then open nothing. Keys that no longer authenticate stay as they are (already unreadable).
    /// </summary>
    private byte[] RekeyNotes(SqliteConnection db, byte[] oldMaster)
    {
        var master = SecretMemory.Random(32);
        RewrapIntegrityKey(db, oldMaster, master);
        var rows = new List<(string Id, byte[] Wrapped)>();
        using (var cmd = Command(db, "SELECT Id,WrappedKey FROM Notes WHERE Protected=1 AND WrappedKey IS NOT NULL"))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) rows.Add((r.GetString(0), (byte[])r[1]));
        foreach (var (id, wrapped) in rows)
        {
            byte[] noteKey;
            try { noteKey = NoteCipher.Decrypt(oldMaster, wrapped, Context(id, "key")); }
            catch (CryptographicException) { continue; }
            try { Execute(db, "UPDATE Notes SET WrappedKey=$key WHERE Id=$id", ("$id", id), ("$key", NoteCipher.Encrypt(master, noteKey, Context(id, "key")))); }
            finally { CryptographicOperations.ZeroMemory(noteKey); }
        }

        return master;
    }

    /// <summary>
    /// Runs a credential change in one transaction, then scrubs the WAL and free pages so the previous
    /// wrappers do not survive in the main file. A failed scrub stays pending and is retried later.
    /// </summary>
    private async Task<T> CredentialMutation<T>(Func<SqliteConnection, (T Result, byte[]? Master)> action, bool install)
    {
        await InitializeAsync().ConfigureAwait(false);
        await writer.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                using var db = Open();
                (T Result, byte[]? Master) outcome;
                using (var tx = db.BeginTransaction())
                {
                    outcome = action(db);
                    if (outcome.Master is null) return outcome.Result;
                    Execute(db, "UPDATE Protection SET Maintenance=1 WHERE Id=1");
                    Commit(db, tx, outcome.Master);
                }

                try { Scrub(db); }
                catch (Exception ex) when (ex is IOException or SqliteException) { }
                if (install) InstallSession(db, outcome.Master);
                else ReplaceSessionMaster(outcome.Master);
                return outcome.Result;
            }).ConfigureAwait(false);
        }
        finally
        {
            writer.Release();
            ProtectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // Note keys do not change on rotation, so an open session only needs the new master key.
    private void ReplaceSessionMaster(byte[] master)
    {
        var previous = masterKey;
        if (previous is null) { CryptographicOperations.ZeroMemory(master); return; }
        masterKey = SecretMemory.Adopt(master);
        CryptographicOperations.ZeroMemory(previous);
    }

    private byte[] Authenticate(SqliteConnection db, string password)
    {
        using var cmd = Command(db, "SELECT Salt,Iterations,PasswordKey FROM Protection WHERE Id=1");
        using var r = cmd.ExecuteReader();
        if (!r.Read() || r.IsDBNull(0)) throw new ProtectionAuthenticationException();
        var derived = NoteCipher.PasswordKey(password, (byte[])r[0], r.GetInt32(1));
        try { return NoteCipher.Decrypt(derived, (byte[])r[2], Context("vault", "password")); }
        catch (CryptographicException) { throw new ProtectionAuthenticationException(); }
        finally { CryptographicOperations.ZeroMemory(derived); }
    }

    public async Task UnlockAsync(string password)
    {
        await Read(db => { InstallSession(db, Authenticate(db, password)); return true; });
        ProtectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void InstallSession(SqliteConnection db, byte[] key)
    {
        ClearSession();
        masterKey = SecretMemory.Adopt(key);
        try
        {
            RebuildPrivateCatalog(db);
            VerifyIntegrity(db, masterKey);
        }
        catch { ClearSession(); throw; }
        Interlocked.Increment(ref sessionVersion);
    }

    private void ClearSession()
    {
        foreach (var pending in pendingCredentials) pending.Dispose();
        pendingCredentials.Clear();
        Interlocked.Increment(ref sessionVersion);
        var previous = masterKey;
        masterKey = null;
        if (previous is not null) CryptographicOperations.ZeroMemory(previous);
        foreach (var key in noteKeys.Values) CryptographicOperations.ZeroMemory(key);
        noteKeys.Clear();
        IntegrityReport = null;
        if (memory is not null) Execute(memory, "DELETE FROM Metadata; DELETE FROM Search; DELETE FROM Links; DELETE FROM Tags; DELETE FROM Categories;");
    }

    /// <summary>
    /// Deletes the archive file with every note, revision, attachment and protection credential,
    /// then creates an empty archive at the same path.
    /// </summary>
    public async Task ResetAsync()
    {
        try { await InitializeAsync().ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or SqliteException or InvalidDataException) { }
        await writer.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                ClearSession();
                ForgetAnchor();
                memory?.Dispose();
                memory = null;
                IsProtectionConfigured = false;
                hideDetails = false;
                archiveId = "";
                SqliteConnection.ClearAllPools();
                foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
            }).ConfigureAwait(false);
            Task created;
            lock (initializationLock) created = initialization = InitializeCoreAsync();
            await created.ConfigureAwait(false);
        }
        finally { writer.Release(); }
        ProtectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task LockAsync()
    {
        await writer.WaitAsync().ConfigureAwait(false);
        try { await Task.Run(ClearSession).ConfigureAwait(false); }
        finally { writer.Release(); }
        ProtectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ChangePasswordAsync(string oldPassword, string newPassword)
    {
        if (!await TryChangePasswordAsync(oldPassword, newPassword)) throw new ProtectionAuthenticationException();
    }

    /// <summary>
    /// Replaces the password and rotates the master key when the recovery secret is available, so the
    /// old password cannot open current data even from a copy of the database file.
    /// </summary>
    public Task<bool> TryChangePasswordAsync(string current, string replacement)
    {
        NoteCipher.ValidatePassword(replacement);
        return CredentialMutation(db =>
        {
            var key = TryAuthenticate(db, current);
            if (key is null) return (false, null);
            try
            {
                var recovery = RecoverySecret(db, key);
                if (recovery is null)
                {
                    // Archives created before recovery escrow cannot rewrap recovery; keep the master key.
                    StorePassword(db, replacement, key);
                    return (true, key.ToArray());
                }

                try
                {
                    var master = RekeyNotes(db, key);
                    StorePassword(db, replacement, master);
                    StoreRecoveryWrappers(db, master, recovery);
                    return (true, master);
                }
                finally { CryptographicOperations.ZeroMemory(recovery); }
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }, install: false);
    }

    public async Task<string> RecoverAsync(string recoveryCode, string newPassword)
    {
        NoteCipher.ValidatePassword(newPassword);
        var code = await CredentialMutation(db =>
        {
            var recovery = NoteCipher.RecoveryKey(recoveryCode);
            byte[] key;
            try { key = NoteCipher.Decrypt(recovery, (byte[])Scalar(db, "SELECT RecoveryKey FROM Protection WHERE Id=1")!, Context("vault", "recovery")); }
            catch (CryptographicException) { throw new ProtectionAuthenticationException(); }
            finally { CryptographicOperations.ZeroMemory(recovery); }
            try
            {
                var master = RekeyNotes(db, key);
                StorePassword(db, newPassword, master);
                return (StoreRecovery(db, master), master);
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }, install: true);
        return code;
    }

    public Task<string> RenewRecoveryCodeAsync(string password) => CredentialMutation(db =>
    {
        var key = Authenticate(db, password);
        try
        {
            var master = RekeyNotes(db, key);
            StorePassword(db, password, master);
            return (StoreRecovery(db, master), master);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }, install: false);

    // Only the Windows adapter supplies wrapping; raw key material is never returned to UI callers.
    public Task<byte[]> WrapForDeviceAsync(string password, Func<byte[], byte[]> wrap) => Read(db =>
    {
        var key = Authenticate(db, password);
        try { return wrap(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    });

    public Task<string> ArchiveIdentityAsync() => Read(db => archiveId);
    public async Task UnlockWithDeviceAsync(Func<byte[]> unwrap)
    {
        await Read(db =>
        {
            var key = unwrap();
            try
            {
                if (key.Length != 32) throw new ProtectionAuthenticationException();
                if (!VerifierMatches(db, key)) throw new ProtectionAuthenticationException();
                InstallSession(db, key);
                return true;
            }
            catch { CryptographicOperations.ZeroMemory(key); throw; }
        });
        ProtectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private byte[] NoteKey(SqliteConnection db, string id)
    {
        _ = Master;
        if (noteKeys.TryGetValue(id, out var existing)) return SecretMemory.Copy(existing);
        var key = NoteCipher.Decrypt(Master, (byte[])(Scalar(db, "SELECT WrappedKey FROM Notes WHERE Id=$id AND Protected=1", ("$id", id)) ?? throw new KeyNotFoundException()), Context(id, "key"));
        noteKeys[id] = SecretMemory.Copy(key, resident: true);
        return key;
    }
    private bool Protected(SqliteConnection db, string id) => Convert.ToBoolean(Scalar(db, "SELECT Protected FROM Notes WHERE Id=$id", ("$id", id)) ?? false);
    private void RequireAccess(SqliteConnection db, string id) { if (Protected(db, id) && !IsUnlocked) throw new NoteLockedException(); }
    private T WithNoteKey<T>(SqliteConnection db, string id, Func<byte[], T> action)
    {
        var key = NoteKey(db, id);
        try { return action(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private PrivateMetadata ReadMetadata(SqliteConnection db, string id) => WithNoteKey(db, id, key =>
        JsonSerializer.Deserialize<PrivateMetadata>(NoteCipher.DecryptText(key,
            (byte[])Scalar(db, "SELECT Metadata FROM Notes WHERE Id=$id", ("$id", id))!, Context(id, "metadata")))!);

    private void PutMetadata(SqliteConnection db, string id, PrivateMetadata value)
    {
        if (!hideDetails && value.Category is not null)
            value = value with { CategoryId = EnsureLabel(db, "Categories", value.Category) };
        WithNoteKey(db, id, key => Execute(db, "UPDATE Notes SET Metadata=$data WHERE Id=$id",
            ("$id", id), ("$data", NoteCipher.EncryptText(key, JsonSerializer.Serialize(value), Context(id, "metadata")))));
        PublishMetadata(db, id, value);
        IndexPrivateMetadata(db, id, value);
    }

    private void PublishMetadata(SqliteConnection db, string id, PrivateMetadata value)
    {
        var hidden = hideDetails && Protected(db, id);
        var categoryId = !hidden && value.Category is not null ? EnsureLabel(db, "Categories", value.Category) : null;
        Execute(db, "UPDATE Notes SET Title=$title,Favorite=$favorite,Color=$color,CategoryId=$category WHERE Id=$id",
            ("$id", id), ("$title", hidden ? "" : value.Title), ("$favorite", !hidden && value.Favorite),
            ("$color", hidden ? 0 : (int)value.Color), ("$category", categoryId));
        Execute(db, "DELETE FROM NoteTags WHERE NoteId=$id", ("$id", id));
        if (!hidden)
            foreach (var tag in value.Tags)
                Execute(db, "INSERT INTO NoteTags(NoteId,TagId) VALUES($id,$tag)", ("$id", id), ("$tag", EnsureLabel(db, "Tags", tag)));
    }

    private void IndexPrivateMetadata(SqliteConnection db, string id, PrivateMetadata value)
    {
        var previous = Scalar(db, "SELECT rowid FROM session.Metadata WHERE Id=$id", ("$id", id));
        if (previous is not null) Execute(db, "DELETE FROM session.Search WHERE rowid=$row", ("$row", previous));
        Execute(db, "INSERT INTO session.Metadata VALUES($id,$title,$favorite,$color,$categoryId,$category,$tags) ON CONFLICT(Id) DO UPDATE SET Title=excluded.Title,Favorite=excluded.Favorite,Color=excluded.Color,CategoryId=excluded.CategoryId,Category=excluded.Category,Tags=excluded.Tags",
            ("$id", id), ("$title", value.Title), ("$favorite", value.Favorite), ("$color", (int)value.Color),
            ("$categoryId", value.CategoryId), ("$category", value.Category), ("$tags", JsonSerializer.Serialize(value.Tags)));
        var row = Scalar(db, "SELECT rowid FROM session.Metadata WHERE Id=$id", ("$id", id));
        Execute(db, "INSERT INTO session.Search(rowid,Id,Title,Tags,Category) VALUES($row,$id,$title,$tags,$category)", ("$row", row),
            ("$id", id), ("$title", value.Title), ("$tags", string.Join(' ', value.Tags)), ("$category", value.Category ?? ""));
        Execute(db, "DELETE FROM session.Tags WHERE NoteId=$id", ("$id", id));
        foreach (var tag in value.Tags)
            Execute(db, "INSERT INTO session.Tags VALUES($id,$name,$normalized)", ("$id", id), ("$name", tag), ("$normalized", Normalize(tag)));
        if (value.CategoryId is not null)
            Execute(db, "INSERT OR REPLACE INTO session.Categories VALUES($id,$name)", ("$id", value.CategoryId), ("$name", value.Category));
        Execute(db, "DELETE FROM session.Links WHERE NoteId=$id", ("$id", id));
        foreach (var link in value.Links ?? [])
            Execute(db, "INSERT INTO session.Links VALUES($id,$target,$label,$line)", ("$id", id), ("$target", link.Target), ("$label", link.Label), ("$line", link.Line));

    }

    /// <summary>Protected notes whose key or metadata failed authentication during the last unlock.</summary>
    public IReadOnlyCollection<string> DamagedNoteIds => damagedNoteIds.ToArray();
    private readonly HashSet<string> damagedNoteIds = new(StringComparer.Ordinal);

    private void RebuildPrivateCatalog(SqliteConnection db)
    {
        Execute(db, "DELETE FROM session.Metadata; DELETE FROM session.Search; DELETE FROM session.Links; DELETE FROM session.Tags; DELETE FROM session.Categories;");
        damagedNoteIds.Clear();
        foreach (var id in ProtectedIds(db))
        {
            // One corrupted record must not lock the user out of every other note.
            try { IndexPrivateMetadata(db, id, ReadMetadata(db, id)); }
            catch (Exception ex) when (ex is CryptographicException or JsonException or KeyNotFoundException or InvalidCastException or NullReferenceException)
            {
                damagedNoteIds.Add(id);
            }
        }
    }
    private static List<string> ProtectedIds(SqliteConnection db)
    {
        using var cmd = Command(db, "SELECT Id FROM Notes WHERE Protected=1");
        using var r = cmd.ExecuteReader();
        var ids = new List<string>();
        while (r.Read()) ids.Add(r.GetString(0));
        return ids;
    }

    public async Task SetProtectionSettingsAsync(bool hideDetails, int idleMinutes)
    {
        if (idleMinutes is not (1 or 5 or 15 or 30)) throw new ArgumentOutOfRangeException(nameof(idleMinutes));
        await SecurityMutation(db =>
        {
            _ = Master;
            this.hideDetails = hideDetails;
            Execute(db, "UPDATE Protection SET HideDetails=$hide,IdleMinutes=$minutes WHERE Id=1", ("$hide", hideDetails), ("$minutes", idleMinutes));
            foreach (var id in ProtectedIds(db).Except(damagedNoteIds)) PutMetadata(db, id, ReadMetadata(db, id));
            if (hideDetails) CleanPrivateLabels(db);
        });
    }

    private void CleanPrivateLabels(SqliteConnection db)
    {
        foreach (var id in ProtectedIds(db).Except(damagedNoteIds))
        {
            var metadata = ReadMetadata(db, id);
            if (metadata.CategoryId is not null)
                Execute(db, "DELETE FROM Categories WHERE Id=$id AND NOT EXISTS(SELECT 1 FROM Notes WHERE CategoryId=$id)", ("$id", metadata.CategoryId));
            foreach (var tag in metadata.Tags)
                Execute(db, "DELETE FROM Tags WHERE Normalized=$name AND Id NOT IN(SELECT TagId FROM NoteTags)", ("$name", Normalize(tag)));
        }
    }

    private async Task SecurityMutation(Action<SqliteConnection> action)
    {
        await InitializeAsync();
        await writer.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var db = Open();
                try
                {
                    using (var tx = db.BeginTransaction())
                    {
                        action(db);
                        Execute(db, "UPDATE Protection SET Maintenance=1 WHERE Id=1");
                        Commit(db, tx);
                    }
                    Scrub(db);
                }
                finally
                {
                    hideDetails = Convert.ToBoolean(Scalar(db, "SELECT HideDetails FROM Protection WHERE Id=1"));
                    if (IsUnlocked) RebuildPrivateCatalog(db);
                }
            });
        }
        finally { writer.Release(); ProtectionChanged?.Invoke(this, EventArgs.Empty); }
    }

    public Task CompleteProtectionMaintenanceAsync() => Read(db =>
    {
        if (Convert.ToBoolean(Scalar(db, "SELECT Maintenance FROM Protection WHERE Id=1"))) Scrub(db);
        return true;
    });

    private static void Scrub(SqliteConnection db)
    {
        Execute(db, "INSERT INTO NoteSearch(NoteSearch) VALUES('rebuild'); INSERT INTO PublicMetadataSearch(PublicMetadataSearch) VALUES('rebuild');");
        CheckpointWal(db);
        Execute(db, "VACUUM;");
        CheckpointWal(db);
        Execute(db, "UPDATE Protection SET Maintenance=0 WHERE Id=1;");
        CheckpointWal(db);
    }
    private static void CheckpointWal(SqliteConnection db)
    {
        using var cmd = Command(db, "PRAGMA wal_checkpoint(TRUNCATE)");
        using var r = cmd.ExecuteReader();
        if (!r.Read() || r.GetInt32(0) != 0) throw new IOException("Archive maintenance must be retried.");
    }

    // Loads the note key from the database when it is not cached, so sealing cannot fail on a cache miss.
    public Task<SealedNoteDraft> SealDraftAsync(string id, string markdown, long version) => Read(db =>
        new SealedNoteDraft(id, version, WithNoteKey(db, id, key => NoteCipher.EncryptText(key, markdown, Context(id, "pending:" + version)))));
    public Task<string> UnsealDraftAsync(SealedNoteDraft draft) => Read(db => WithNoteKey(db, draft.NoteId,
        key => NoteCipher.DecryptText(key, draft.Data, Context(draft.NoteId, "pending:" + draft.Version))));
}

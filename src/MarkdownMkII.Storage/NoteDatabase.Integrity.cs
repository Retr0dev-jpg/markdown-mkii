using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage;

/// <summary>
/// Detects protected records put back, swapped or removed outside the app. Integrity holds a digest of
/// every encrypted record; triggers queue the records a transaction touches, and before each commit
/// their digests are refreshed and sealed with an HMAC whose key only the master key opens. The sealed
/// generation is mirrored to an anchor outside the file.
/// </summary>
public sealed partial class NoteDatabase
{
    /// <summary>External store of the last sealed generation; without it only single records are checked.</summary>
    public IIntegrityAnchor? Anchor { get; set; }

    /// <summary>Set by an unlock that found tampering; protected writes are refused until it is accepted or the archive is locked.</summary>
    public ArchiveIntegrityReport? IntegrityReport { get; private set; }

    private byte[]? pendingAnchor;
    private const int AnchorLength = 8 + 32 + 32;

    private const string LeafRows = """
        SELECT 'n' AS Kind,Id,Id AS Owner,mkii_digest(Cipher,Metadata) AS Digest FROM Notes WHERE Protected=1
        UNION ALL SELECT 'r',NoteId||':'||Id,NoteId,mkii_digest(Cipher,NULL) FROM Revisions WHERE Cipher IS NOT NULL
        UNION ALL SELECT 'a',OwnerId||':'||Id,OwnerId,mkii_digest(Data,CipherName) FROM Attachments WHERE OwnerId IS NOT NULL
        """;

    // Row is the rowid inside the same transaction, so refreshing a queued digest needs no scan.
    private const string RefreshQueued = """
        DELETE FROM Integrity WHERE EXISTS(SELECT 1 FROM IntegrityPending p WHERE p.Kind=Integrity.Kind AND p.Id=Integrity.Id);
        INSERT OR REPLACE INTO Integrity SELECT p.Kind,p.Id,mkii_digest(n.Cipher,n.Metadata) FROM IntegrityPending p
            JOIN Notes n ON n.Id=p.Id WHERE p.Kind='n' AND n.Protected=1;
        INSERT OR REPLACE INTO Integrity SELECT p.Kind,p.Id,mkii_digest(r.Cipher,NULL) FROM IntegrityPending p
            JOIN Revisions r ON r.Id=p.Row WHERE p.Kind='r' AND r.Cipher IS NOT NULL AND r.NoteId||':'||r.Id=p.Id;
        INSERT OR REPLACE INTO Integrity SELECT p.Kind,p.Id,mkii_digest(a.Data,a.CipherName) FROM IntegrityPending p
            JOIN Attachments a ON a.rowid=p.Row WHERE p.Kind='a' AND a.OwnerId IS NOT NULL AND a.OwnerId||':'||a.Id=p.Id;
        DELETE FROM IntegrityPending;
        """;

    private static void MigrateIntegrity(SqliteConnection db)
    {
        using var tx = db.BeginTransaction();
        if (Convert.ToInt32(Scalar(db, "PRAGMA user_version")) == 2)
            Execute(db, $"""
                ALTER TABLE Protection ADD COLUMN IntegrityKey BLOB;
                ALTER TABLE Protection ADD COLUMN IntegritySeal BLOB;
                ALTER TABLE Protection ADD COLUMN IntegrityGeneration INTEGER NOT NULL DEFAULT 0;
                CREATE TABLE Integrity(Kind TEXT NOT NULL,Id TEXT NOT NULL,Digest BLOB NOT NULL,PRIMARY KEY(Kind,Id)) WITHOUT ROWID;
                CREATE TABLE IntegrityPending(Kind TEXT NOT NULL,Id TEXT NOT NULL,Row INTEGER NOT NULL,PRIMARY KEY(Kind,Id)) WITHOUT ROWID;
                INSERT INTO Integrity(Kind,Id,Digest) SELECT Kind,Id,Digest FROM ({LeafRows});
                PRAGMA user_version=3;
                """);
        // Recreated on every open, so dropped or edited triggers cannot silently stop digest upkeep.
        Execute(db, IntegrityTriggers);
        tx.Commit();
    }

    // Plain SQL only: tools that edit unprotected notes keep working without the app's functions.
    private static readonly (string Name, string Body)[] Triggers =
    [
        ("IntegrityNoteInsert", "AFTER INSERT ON Notes WHEN new.Protected=1 BEGIN INSERT OR IGNORE INTO IntegrityPending VALUES('n',new.Id,new.rowid); END"),
        ("IntegrityNoteUpdate", "AFTER UPDATE OF Protected,Cipher,Metadata ON Notes WHEN old.Protected=1 OR new.Protected=1 BEGIN INSERT OR IGNORE INTO IntegrityPending VALUES('n',new.Id,new.rowid); END"),
        ("IntegrityNoteDelete", "AFTER DELETE ON Notes WHEN old.Protected=1 BEGIN INSERT OR IGNORE INTO IntegrityPending VALUES('n',old.Id,old.rowid); END"),
        ("IntegrityRevisionInsert", "AFTER INSERT ON Revisions WHEN new.Cipher IS NOT NULL BEGIN INSERT OR IGNORE INTO IntegrityPending VALUES('r',new.NoteId||':'||new.Id,new.Id); END"),
        ("IntegrityRevisionUpdate", "AFTER UPDATE OF Cipher ON Revisions WHEN old.Cipher IS NOT NULL OR new.Cipher IS NOT NULL BEGIN INSERT OR IGNORE INTO IntegrityPending VALUES('r',new.NoteId||':'||new.Id,new.Id); END"),
        ("IntegrityRevisionDelete", "AFTER DELETE ON Revisions WHEN old.Cipher IS NOT NULL BEGIN INSERT OR IGNORE INTO IntegrityPending VALUES('r',old.NoteId||':'||old.Id,old.Id); END"),
        ("IntegrityAttachmentInsert", "AFTER INSERT ON Attachments WHEN new.OwnerId IS NOT NULL BEGIN INSERT OR IGNORE INTO IntegrityPending VALUES('a',new.OwnerId||':'||new.Id,new.rowid); END"),
        ("IntegrityAttachmentUpdate", "AFTER UPDATE OF Data,CipherName,OwnerId ON Attachments WHEN old.OwnerId IS NOT NULL OR new.OwnerId IS NOT NULL BEGIN INSERT OR IGNORE INTO IntegrityPending SELECT 'a',old.OwnerId||':'||old.Id,old.rowid WHERE old.OwnerId IS NOT NULL; INSERT OR IGNORE INTO IntegrityPending SELECT 'a',new.OwnerId||':'||new.Id,new.rowid WHERE new.OwnerId IS NOT NULL; END"),
        ("IntegrityAttachmentDelete", "AFTER DELETE ON Attachments WHEN old.OwnerId IS NOT NULL BEGIN INSERT OR IGNORE INTO IntegrityPending VALUES('a',old.OwnerId||':'||old.Id,old.rowid); END"),
    ];

    private static readonly string IntegrityTriggers = string.Concat(Triggers.Select(t => $"DROP TRIGGER IF EXISTS {t.Name}; CREATE TRIGGER {t.Name} {t.Body};"));

    internal static byte[] IntegrityDigest(byte[]? first, byte[]? second)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendField(hash, first);
        AppendField(hash, second);
        return hash.GetHashAndReset();
    }

    private static void AppendField(IncrementalHash hash, byte[]? value)
    {
        Span<byte> length = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(length, value?.Length ?? -1);
        hash.AppendData(length);
        if (value is not null) hash.AppendData(value);
    }

    /// <summary>Seals protected changes, commits, then mirrors the new generation outside the file.</summary>
    private void Commit(SqliteConnection db, SqliteTransaction tx, byte[]? master = null)
    {
        pendingAnchor = null;
        SealIntegrity(db, master);
        tx.Commit();
        PublishAnchor();
    }

    // While locked the app cannot change protected records, so queued rows come from outside the app:
    // they stay unsealed and the next unlock reports them.
    private void SealIntegrity(SqliteConnection db, byte[]? master)
    {
        if (Scalar(db, "SELECT 1 FROM IntegrityPending LIMIT 1") is null) return;
        if ((master ?? masterKey) is not { } key) return;
        if (IntegrityReport is not null) throw new ArchiveIntegrityException();
        Reseal(db, key, accept: false);
    }

    /// <summary>
    /// Signs the current digests under the next generation. When accepting, the digests are rebuilt from
    /// all records instead of the queued ones and the generation also moves past the anchor.
    /// </summary>
    private void Reseal(SqliteConnection db, byte[] master, bool accept)
    {
        var key = IntegrityKey(db, master, accept);
        try
        {
            if (accept)
                Execute(db, $"DELETE FROM IntegrityPending; DELETE FROM Integrity; INSERT INTO Integrity(Kind,Id,Digest) SELECT Kind,Id,Digest FROM ({LeafRows});");
            else
                Execute(db, RefreshQueued);
            var generation = Convert.ToInt64(Scalar(db, "SELECT IntegrityGeneration FROM Protection WHERE Id=1"));
            if (accept && ReadAnchor(key) is { } anchor) generation = Math.Max(generation, anchor.Generation);
            generation++;
            var seal = ComputeSeal(db, key, generation);
            Execute(db, "UPDATE Protection SET IntegrityGeneration=$generation,IntegritySeal=$seal WHERE Id=1",
                ("$generation", generation), ("$seal", seal));
            pendingAnchor = AnchorValue(key, generation, seal);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private byte[] IntegrityKey(SqliteConnection db, byte[] master, bool replaceUnreadable)
    {
        if (Scalar(db, "SELECT IntegrityKey FROM Protection WHERE Id=1") is byte[] wrapped)
        {
            if (TryUnwrap(master, wrapped, Context("vault", "integrity")) is { } existing) return existing;
            if (!replaceUnreadable) throw new ArchiveIntegrityException();
        }
        var key = SecretMemory.Random(32);
        // The sealed verifier records that a seal exists, so stripping the key later is detected.
        Execute(db, "UPDATE Protection SET IntegrityKey=$key,Verifier=$verifier WHERE Id=1",
            ("$key", NoteCipher.Encrypt(master, key, Context("vault", "integrity"))),
            ("$verifier", NoteCipher.EncryptText(master, archiveId, Context("vault", "verify:sealed"))));
        return key;
    }

    private void RewrapIntegrityKey(SqliteConnection db, byte[] oldMaster, byte[] master)
    {
        if (Scalar(db, "SELECT IntegrityKey FROM Protection WHERE Id=1") is not byte[] wrapped) return;
        if (TryUnwrap(oldMaster, wrapped, Context("vault", "integrity")) is not { } key) return;
        try { Execute(db, "UPDATE Protection SET IntegrityKey=$key WHERE Id=1", ("$key", NoteCipher.Encrypt(master, key, Context("vault", "integrity")))); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private string VerifierContext(SqliteConnection db)
        => Context("vault", Scalar(db, "SELECT IntegrityKey FROM Protection WHERE Id=1") is byte[] ? "verify:sealed" : "verify");

    private bool VerifierMatches(SqliteConnection db, byte[] key, bool sealedOnly = false)
    {
        var verifier = (byte[])Scalar(db, "SELECT Verifier FROM Protection WHERE Id=1")!;
        foreach (var kind in sealedOnly ? new[] { "verify:sealed" } : ["verify:sealed", "verify"])
        {
            if (TryUnwrap(key, verifier, Context("vault", kind)) is not { } value) continue;
            try { return Encoding.UTF8.GetString(value) == archiveId; }
            finally { CryptographicOperations.ZeroMemory(value); }
        }
        return false;
    }

    private byte[] ComputeSeal(SqliteConnection db, byte[] key, long generation)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
        AppendField(hmac, Encoding.UTF8.GetBytes("MarkdownMkII:integrity:" + archiveId + ":" + generation));
        using var cmd = Command(db, "SELECT Kind,Id,Digest FROM Integrity ORDER BY Kind,Id");
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AppendField(hmac, Encoding.UTF8.GetBytes(r.GetString(0)));
            AppendField(hmac, Encoding.UTF8.GetBytes(r.GetString(1)));
            AppendField(hmac, (byte[])r[2]);
        }
        return hmac.GetHashAndReset();
    }

    /// <summary>
    /// Runs on every unlock. Archives never sealed before (created before this check) are trusted as they
    /// are and sealed now; otherwise the seal, every record digest and the external anchor must agree.
    /// </summary>
    private void VerifyIntegrity(SqliteConnection db, byte[] master)
    {
        IntegrityReport = null;
        pendingAnchor = null;
        // Detection compares digests with the records; rows queued outside the app add nothing and would
        // otherwise block unrelated writes while a report is pending.
        Execute(db, "DELETE FROM IntegrityPending");
        if (Scalar(db, "SELECT IntegrityKey FROM Protection WHERE Id=1") is not byte[] wrapped)
        {
            if (VerifierMatches(db, master, sealedOnly: true)) { IntegrityReport = new(false, true, []); return; }
            using (var tx = db.BeginTransaction())
            {
                Reseal(db, master, accept: true);
                tx.Commit();
            }
            PublishAnchor();
            return;
        }

        if (TryUnwrap(master, wrapped, Context("vault", "integrity")) is not { } key) { IntegrityReport = new(false, true, []); return; }
        try
        {
            var generation = Convert.ToInt64(Scalar(db, "SELECT IntegrityGeneration FROM Protection WHERE Id=1"));
            if (Scalar(db, "SELECT IntegritySeal FROM Protection WHERE Id=1") is not byte[] seal
                || !CryptographicOperations.FixedTimeEquals(seal, ComputeSeal(db, key, generation)))
            {
                IntegrityReport = new(false, true, []);
                return;
            }

            var changed = ChangedNotes(db);
            var anchor = ReadAnchor(key);
            var rolledBack = anchor is { } stored && (stored.Generation > generation
                || stored.Generation == generation && !CryptographicOperations.FixedTimeEquals(stored.Seal, seal));
            if (rolledBack || changed.Count > 0) { IntegrityReport = new(rolledBack, false, changed); return; }
            // A newer file than the anchor means the anchor write was missed after a commit.
            if (anchor is null || anchor.Value.Generation < generation)
            {
                pendingAnchor = AnchorValue(key, generation, seal);
                PublishAnchor();
            }
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static List<string> ChangedNotes(SqliteConnection db)
    {
        var leaves = new Dictionary<(string Kind, string Id), byte[]>();
        using (var cmd = Command(db, "SELECT Kind,Id,Digest FROM Integrity"))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) leaves[(r.GetString(0), r.GetString(1))] = (byte[])r[2];
        var changed = new HashSet<string>(StringComparer.Ordinal);
        using (var cmd = Command(db, LeafRows))
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                if (!leaves.Remove((r.GetString(0), r.GetString(1)), out var expected) || !expected.AsSpan().SequenceEqual((byte[])r[3]))
                    changed.Add(r.GetString(2));
        foreach (var (kind, id) in leaves.Keys)
            changed.Add(kind == "n" ? id : id[..Math.Max(0, id.IndexOf(':'))]);
        return changed.ToList();
    }

    /// <summary>Accepts the archive as it is now after the user reviewed an integrity report.</summary>
    public async Task AcceptIntegrityAsync()
    {
        await InitializeAsync().ConfigureAwait(false);
        await writer.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                var master = Master;
                using var db = Open();
                pendingAnchor = null;
                using (var tx = db.BeginTransaction())
                {
                    Reseal(db, master, accept: true);
                    tx.Commit();
                }
                IntegrityReport = null;
                PublishAnchor();
            }).ConfigureAwait(false);
        }
        finally { writer.Release(); }
        ProtectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private byte[] AnchorValue(byte[] key, long generation, byte[] seal)
    {
        var value = new byte[AnchorLength];
        BinaryPrimitives.WriteInt64LittleEndian(value, generation);
        seal.CopyTo(value, 8);
        AnchorMac(key, value.AsSpan(0, 40)).CopyTo(value, 40);
        return value;
    }

    private byte[] AnchorMac(byte[] key, ReadOnlySpan<byte> body)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
        hmac.AppendData(Encoding.UTF8.GetBytes("MarkdownMkII:anchor:" + archiveId));
        hmac.AppendData(body);
        return hmac.GetHashAndReset();
    }

    // An unreadable or forged anchor counts as missing: it can only be removed, never faked forward.
    private (long Generation, byte[] Seal)? ReadAnchor(byte[] key)
    {
        byte[]? value;
        try { value = Anchor?.Load(archiveId); }
        catch (Exception) { return null; }
        if (value is not { Length: AnchorLength }
            || !CryptographicOperations.FixedTimeEquals(value.AsSpan(40), AnchorMac(key, value.AsSpan(0, 40))))
            return null;
        return (BinaryPrimitives.ReadInt64LittleEndian(value), value[8..40]);
    }

    // A missed write is harmless: the next unlock finds the file ahead of the anchor and accepts it.
    private void PublishAnchor()
    {
        var value = pendingAnchor;
        pendingAnchor = null;
        if (value is null || Anchor is null || archiveId.Length == 0) return;
        try { Anchor.Store(archiveId, value); }
        catch (Exception) { }
    }

    private void ForgetAnchor()
    {
        if (Anchor is null || archiveId.Length == 0) return;
        try { Anchor.Forget(archiveId); }
        catch (Exception) { }
    }
}

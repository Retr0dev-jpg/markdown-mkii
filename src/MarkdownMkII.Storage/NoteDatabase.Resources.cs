using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownMkII.Core.Markdown;
using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage;
public sealed partial class NoteDatabase
{
    public static IReadOnlyList<NoteLink> ExtractLinks(string markdown) => MarkdownReference.Parse(markdown).Where(r => !r.Image).Select(r => new NoteLink(NormalizeLinkTarget(r.Target), r.Label, r.Line)).ToArray();
    private static string NormalizeLinkTarget(string target)
    {
        target = Uri.UnescapeDataString(target.Split('#')[0]);
        if (target.StartsWith("wiki:", StringComparison.OrdinalIgnoreCase))
            target = target[5..].TrimStart('/');
        if (target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            target = target[..^3];
        return target;
    }

    private static void IndexLinks(SqliteConnection db, string id, string markdown)
    {
        Execute(db, "DELETE FROM NoteLinks WHERE NoteId=$id", ("$id", id));
        foreach (var link in ExtractLinks(markdown))
            Execute(db, "INSERT INTO NoteLinks(NoteId,Target,Label,Line) VALUES($id,$target,$label,$line)", ("$id", id), ("$target", link.Target), ("$label", link.Label), ("$line", link.Line));
    }

    public Task<IReadOnlyList<NoteSummary>> ResolveAsync(string target, CancellationToken token = default) => Read<IReadOnlyList<NoteSummary>>(db =>
    {
        target = NormalizeLinkTarget(target);
        var id = target.StartsWith("note:", StringComparison.OrdinalIgnoreCase) ? target[5..].TrimStart('/') : target;
        var sql = $"SELECT {SummaryColumns} FROM Catalog n LEFT JOIN CatalogCategories c ON c.Id=n.CategoryId WHERE n.Trashed=0 AND (n.Id=$id OR n.Title=$title COLLATE NOCASE) ORDER BY n.Modified DESC LIMIT 100";
        using var cmd = Command(db, sql, ("$id", id), ("$title", target));
        using var r = cmd.ExecuteReader();
        var result = new List<NoteSummary>();
        while (r.Read())
            result.Add(Summary(r));
        return result;
    }, token);
    public Task<IReadOnlyList<(string Id, string Title, int Line)>> BacklinksAsync(string id, string title) => Read<IReadOnlyList<(string, string, int)>>(db =>
    {
        using var cmd = Command(db, "SELECT DISTINCT n.Id,n.Title,l.Line FROM (SELECT NoteId,Target,Label,Line FROM NoteLinks UNION ALL SELECT NoteId,Target,Label,Line FROM session.Links) l JOIN Catalog n ON n.Id=l.NoteId WHERE n.Trashed=0 AND (l.Target=$uri OR l.Target=$short OR l.Target=$title COLLATE NOCASE) LIMIT 200", ("$uri", "note://" + id), ("$short", "note:" + id), ("$title", title));
        using var r = cmd.ExecuteReader();
        var result = new List<(string, string, int)>();
        while (r.Read())
            result.Add((r.GetString(0), r.GetString(1), r.GetInt32(2)));
        return result;
    });
    public async Task<string> AddAttachmentAsync(Stream input, string name, CancellationToken token = default, string? noteId = null)
    {
        // Read and encrypt off the UI thread; no intermediate plaintext file is created.
        return await Write(db =>
        {
            if (noteId is not null) RequireAccess(db, noteId);
            using var buffer = new MemoryStream();
            input.CopyTo(buffer);
            token.ThrowIfCancellationRequested();
            var bytes = buffer.ToArray();
            try
            {
                return noteId is not null && Protected(db, noteId) ? AddProtectedAttachment(db, noteId, name, bytes) : AddPublicAttachment(db, name, bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
                if (buffer.TryGetBuffer(out var backing)) CryptographicOperations.ZeroMemory(backing.AsSpan());
            }
        }, token).ConfigureAwait(false);
    }

    public Task CopyAttachmentAsync(string id, Stream destination, CancellationToken token = default, string? noteId = null) => Read(db =>
    {
        var asset = ReadAttachment(db, id, noteId) ?? throw new FileNotFoundException("Attachment not found");
        // A lock cancels the token; decrypted bytes must not reach the destination after that.
        try { token.ThrowIfCancellationRequested(); destination.Write(asset.Bytes); return true; }
        finally { CryptographicOperations.ZeroMemory(asset.Bytes); }
    }, token);
    public Task<string?> AttachmentNameAsync(string id, string? noteId = null) => Read(db =>
    {
        using var cmd = Command(db, "SELECT Name,OwnerId,CipherName FROM Attachments WHERE Id=$id", ("$id", id));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        if (r.IsDBNull(1)) return r.GetString(0);
        var owner = r.GetString(1);
        if (noteId is not null && owner != noteId) throw new NoteLockedException();
        return WithNoteKey(db, owner, key => NoteCipher.DecryptText(key, (byte[])r[2], Context(owner, "asset-name:" + id)));
    });
    /// <summary>
    /// Resolves a relative image or file reference to an attachment with that file name: first among the
    /// note's own protected attachments, then among public ones, newest first.
    /// </summary>
    public Task<string?> FindAttachmentAsync(string name, string? noteId = null) => Read(db =>
    {
        name = AttachmentName(name);
        if (name.Length == 0) return null;
        if (noteId is not null && Protected(db, noteId) && IsUnlocked)
        {
            var owned = new List<(string Id, byte[] Name)>();
            using (var cmd = Command(db, "SELECT Id,CipherName FROM Attachments WHERE OwnerId=$owner", ("$owner", noteId)))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) owned.Add((r.GetString(0), (byte[])r[1]));
            foreach (var (id, cipher) in owned)
                if (string.Equals(WithNoteKey(db, noteId, key => NoteCipher.DecryptText(key, cipher, Context(noteId, "asset-name:" + id))), name, StringComparison.OrdinalIgnoreCase))
                    return id;
        }

        return Scalar(db, "SELECT Id FROM Attachments WHERE OwnerId IS NULL AND Name=$name COLLATE NOCASE ORDER BY rowid DESC LIMIT 1", ("$name", name)) as string;
    });
    public Task<bool> IsProtectedAttachmentAsync(string id) => Read(db => Scalar(db, "SELECT OwnerId FROM Attachments WHERE Id=$id", ("$id", id)) is string);
    public async Task BackupAsync(string destination)
    {
        await InitializeAsync().ConfigureAwait(false);
        await writer.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                using var db = Open();
                if (Convert.ToBoolean(Scalar(db, "SELECT Maintenance FROM Protection WHERE Id=1"))) Scrub(db);
                using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ToString());
                backup.Open();
                db.BackupDatabase(backup);
            }).ConfigureAwait(false);
        }
        finally
        {
            writer.Release();
        }
    }

    public async Task RestoreAsync(string source)
    {
        await InitializeAsync().ConfigureAwait(false);
        await writer.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                backup.Open();
                var sourceVersion = Convert.ToInt32(Scalar(backup, "PRAGMA user_version"));
                if (sourceVersion is not (1 or 2 or 3) || !Equals(Scalar(backup, "PRAGMA integrity_check"), "ok"))
                    throw new InvalidDataException("Invalid archive.");
                foreach (var(table, columns)in new[]
                {
                    ("Notes", "Id,Title,Markdown,Preview,Created,Modified,Version,Favorite,Color,CategoryId,Archived,Trashed"),
                    ("Categories", "Id,Name,Normalized"),
                    ("Tags", "Id,Name,Normalized"),
                    ("NoteTags", "NoteId,TagId"),
                    ("Revisions", "Id,NoteId,Markdown,Created"),
                    ("Attachments", "Id,Name,Data"),
                    ("NoteLinks", "NoteId,Target,Label,Line"),
                    ("ImportSources", "Source,Hash,NoteId"),
                    ("NoteSearch", "Title,Markdown")
                }

                )
                    using (var check = Command(backup, $"SELECT {columns} FROM {table} LIMIT 0"))
                    using (var reader = check.ExecuteReader())
                    {
                    }

                if (Scalar(backup, "PRAGMA foreign_key_check")is not null)
                    throw new InvalidDataException("Invalid archive associations.");
                if (sourceVersion >= 2)
                    foreach (var (table, columns) in new[] { ("PublicMetadataSearch", "Title,Tags,Category"), ("Protection", "ArchiveId,Salt,Iterations,PasswordKey,RecoveryKey,Verifier,HideDetails,IdleMinutes,Maintenance"), ("Notes", "Protected,Cipher,Metadata,WrappedKey"), ("Revisions", "Cipher"), ("Attachments", "OwnerId,CipherName") })
                        using (var check = Command(backup, $"SELECT {columns} FROM {table} LIMIT 0"))
                        using (var reader = check.ExecuteReader()) { }
                if (sourceVersion == 3)
                    foreach (var (table, columns) in new[] { ("Integrity", "Kind,Id,Digest"), ("IntegrityPending", "Kind,Id,Row"), ("Protection", "IntegrityKey,IntegritySeal,IntegrityGeneration") })
                        using (var check = Command(backup, $"SELECT {columns} FROM {table} LIMIT 0"))
                        using (var reader = check.ExecuteReader()) { }
                using var db = Open();
                ClearSession();
                ForgetAnchor();
                backup.BackupDatabase(db);
                MigrateProtection(db);
                // Restoring is an explicit choice of an older state: the restored seal becomes the new reference.
                ForgetAnchor();
            }).ConfigureAwait(false);
        }
        finally
        {
            writer.Release();
        }
    }
}

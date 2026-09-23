using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage;

public sealed partial class NoteDatabase
{
    public Task<bool> HasProtectedContentAsync(string id) => Read(db =>
    {
        if (Protected(db, id)) return true;
        var text = Scalar(db, "SELECT Markdown FROM Notes WHERE Id=$id", ("$id", id)) as string ?? "";
        if (AttachmentIds(text).Any(asset => Scalar(db, "SELECT OwnerId FROM Attachments WHERE Id=$id", ("$id", asset)) is string)) return true;
        // An ordinary note can embed a protected note; treat external rendering as an export too.
        return MarkdownMkII.Core.Markdown.WikiLinks.Find(text).Any(link => link.IsEmbed);
    });

    private string CanonicalizeProtectedAttachments(SqliteConnection db, string id, string text)
    {
        var metadata = ReadMetadata(db, id);
        var aliases = metadata.AttachmentAliases is null ? new Dictionary<string, string>() : new Dictionary<string, string>(metadata.AttachmentAliases);
        var introduced = new HashSet<string>();
        foreach (var asset in AttachmentIds(text).Distinct())
        {
            if (aliases.ContainsKey(asset) || Equals(Scalar(db, "SELECT OwnerId FROM Attachments WHERE Id=$id", ("$id", asset)), id)) continue;
            var source = ReadAttachment(db, asset, null);
            if (source is null) continue;
            try { aliases[asset] = AddProtectedAttachment(db, id, source.Value.Name, source.Value.Bytes); introduced.Add(asset); }
            finally { CryptographicOperations.ZeroMemory(source.Value.Bytes); }
        }
        var canonical = RewriteAttachments(text, aliases);
        if (introduced.Count > 0)
        {
            PutMetadata(db, id, metadata with { AttachmentAliases = aliases });
            RemoveUnusedPublicAssets(db, introduced);
        }
        return canonical;
    }

    public Task SetPrivateCategoryAsync(string id, string name) => Write(db =>
    {
        RequireAccess(db, id);
        var value = ReadMetadata(db, id);
        name = CleanLabel(name);
        var existing = Scalar(db, "SELECT Id FROM CatalogCategories WHERE normalize_label(Name)=$name LIMIT 1", ("$name", Normalize(name))) as string;
        PutMetadata(db, id, value with { CategoryId = existing ?? Id(), Category = name });
        Execute(db, "UPDATE Notes SET Modified=$now WHERE Id=$id", ("$now", Now()), ("$id", id));
        return true;
    });

    public Task ProtectAsync(string noteId) => SecurityMutation(db => Protect(db, noteId));
    private void Protect(SqliteConnection db, string id)
    {
        _ = Master;
        if (Protected(db, id)) return;
        var note = Get(db, id) ?? throw new KeyNotFoundException();
        var revisions = ReadRevisions(db, id);
        var key = SecretMemory.Random(32);
        try
        {
            if (noteKeys.Remove(id, out var oldKey)) CryptographicOperations.ZeroMemory(oldKey);
            noteKeys[id] = SecretMemory.Copy(key, resident: true);
            Execute(db, "UPDATE Notes SET Protected=1,WrappedKey=$key WHERE Id=$id", ("$id", id), ("$key", NoteCipher.Encrypt(Master, key, Context(id, "key"))));
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var text in revisions.Select(r => r.Markdown).Prepend(note.Markdown))
                foreach (var asset in AttachmentIds(text))
                {
                    if (map.ContainsKey(asset)) continue;
                    var data = ReadAttachment(db, asset, null);
                    if (data is null) continue;
                    try { map[asset] = AddProtectedAttachment(db, id, data.Value.Name, data.Value.Bytes); }
                    finally { CryptographicOperations.ZeroMemory(data.Value.Bytes); }
                }
            var body = RewriteAttachments(note.Markdown, map);
            Execute(db, "UPDATE Notes SET Markdown='',Preview='',Cipher=$cipher,Version=Version+1 WHERE Id=$id",
                ("$id", id), ("$cipher", NoteCipher.EncryptText(key, body, BodyContext(id, note.Version + 1))));
            foreach (var revision in revisions)
                Execute(db, "UPDATE Revisions SET Markdown='',Cipher=$cipher WHERE Id=$revision", ("$revision", revision.Id),
                    ("$cipher", NoteCipher.EncryptText(key, RewriteAttachments(revision.Markdown, map), Context(id, "revision:" + revision.Id))));
            PutMetadata(db, id, new(note.Summary.Title, note.Summary.Favorite, note.Summary.Color, note.Summary.CategoryId, note.Summary.Category, note.Summary.Tags.ToArray(), ExtractLinks(body).ToArray()));
            Execute(db, "DELETE FROM NoteLinks WHERE NoteId=$id; DELETE FROM ImportSources WHERE NoteId=$id", ("$id", id));
            RemoveUnusedPublicAssets(db, map.Keys);
            if (hideDetails) CleanPrivateLabels(db);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public Task UnprotectAsync(string noteId) => SecurityMutation(db => Unprotect(db, noteId));
    private void Unprotect(SqliteConnection db, string noteId)
    {
        if (!Protected(db, noteId)) return;
        var note = Get(db, noteId)!;
        var metadata = ReadMetadata(db, noteId);
        var revisions = ReadRevisions(db, noteId);
        var map = new Dictionary<string, string>();
        foreach (var asset in AttachmentIds(note.Markdown).Concat(revisions.SelectMany(r => AttachmentIds(r.Markdown))).Distinct())
        {
            var data = ReadAttachment(db, asset, noteId);
            if (data is null) continue;
            try { map[asset] = AddPublicAttachment(db, data.Value.Name, data.Value.Bytes); }
            finally { CryptographicOperations.ZeroMemory(data.Value.Bytes); }
        }
        var body = RewriteAttachments(note.Markdown, map);
        Execute(db, "UPDATE Notes SET Protected=0,Markdown=$text,Preview=$preview,Cipher=NULL,Metadata=NULL,WrappedKey=NULL,Version=Version+1 WHERE Id=$id", ("$id", noteId), ("$text", body), ("$preview", Preview(body)));
        foreach (var revision in revisions)
            Execute(db, "UPDATE Revisions SET Markdown=$text,Cipher=NULL WHERE Id=$id", ("$id", revision.Id), ("$text", RewriteAttachments(revision.Markdown, map)));
        var previousHide = hideDetails;
        try { hideDetails = false; PublishMetadata(db, noteId, metadata); }
        finally { hideDetails = previousHide; }
        Execute(db, "DELETE FROM Attachments WHERE OwnerId=$id", ("$id", noteId));
        Execute(db, "DELETE FROM session.Metadata WHERE Id=$id; DELETE FROM session.Search WHERE Id=$id; DELETE FROM session.Links WHERE NoteId=$id; DELETE FROM session.Tags WHERE NoteId=$id", ("$id", noteId));
        IndexLinks(db, noteId, body);
    }

    public Task<NoteDocument> DuplicateAsync(string sourceId, string title) => Write(db => Duplicate(db, sourceId, title));
    private NoteDocument Duplicate(SqliteConnection db, string sourceId, string title)
    {
        var source = Get(db, sourceId) ?? throw new KeyNotFoundException();
        var id = Id();
        var now = Now();
        if (!source.Summary.IsProtected)
        {
            Execute(db, "INSERT INTO Notes(Id,Title,Markdown,Preview,Created,Modified) VALUES($id,$title,$text,$preview,$now,$now)",
                ("$id", id), ("$title", title), ("$text", source.Markdown), ("$preview", Preview(source.Markdown)), ("$now", now));
            PublishMetadata(db, id, new(title, source.Summary.Favorite, source.Summary.Color, source.Summary.CategoryId, source.Summary.Category, source.Summary.Tags.ToArray()));
            Checkpoint(db, id, source.Markdown, true);
            IndexLinks(db, id, source.Markdown);
        }
        else
        {
            var key = SecretMemory.Random(32);
            try
            {
                Execute(db, "INSERT INTO Notes(Id,Title,Markdown,Preview,Created,Modified,Protected,WrappedKey) VALUES($id,'','','',$now,$now,1,$key)",
                    ("$id", id), ("$now", now), ("$key", NoteCipher.Encrypt(Master, key, Context(id, "key"))));
                var map = new Dictionary<string, string>();
                foreach (var asset in AttachmentIds(source.Markdown).Distinct())
                {
                    var data = ReadAttachment(db, asset, sourceId);
                    if (data is null) continue;
                    try { map[asset] = AddProtectedAttachment(db, id, data.Value.Name, data.Value.Bytes); }
                    finally { CryptographicOperations.ZeroMemory(data.Value.Bytes); }
                }
                var body = RewriteAttachments(source.Markdown, map);
                Execute(db, "UPDATE Notes SET Cipher=$cipher WHERE Id=$id", ("$id", id), ("$cipher", NoteCipher.EncryptText(key, body, BodyContext(id, 1))));
                PutMetadata(db, id, new(title, source.Summary.Favorite, source.Summary.Color, source.Summary.CategoryId, source.Summary.Category, source.Summary.Tags.ToArray(), ExtractLinks(body).ToArray()));
                Checkpoint(db, id, body, true);
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        return Get(db, id)!;
    }

    public Task<NoteDocument> CreateProtectedAsync(string title, string markdown, string? sourceId = null) => Write(db =>
    {
        _ = Master;
        var id = Id();
        var key = SecretMemory.Random(32);
        try
        {
            Execute(db, "INSERT INTO Notes(Id,Title,Markdown,Preview,Created,Modified,Protected,WrappedKey) VALUES($id,'','','',$now,$now,1,$key)",
                ("$id", id), ("$now", Now()), ("$key", NoteCipher.Encrypt(Master, key, Context(id, "key"))));
            var map = new Dictionary<string, string>();
            foreach (var asset in AttachmentIds(markdown).Distinct())
            {
                var data = ReadAttachment(db, asset, sourceId);
                if (data is null) continue;
                try { map[asset] = AddProtectedAttachment(db, id, data.Value.Name, data.Value.Bytes); }
                finally { CryptographicOperations.ZeroMemory(data.Value.Bytes); }
            }
            var body = RewriteAttachments(markdown, map);
            Execute(db, "UPDATE Notes SET Cipher=$cipher WHERE Id=$id", ("$id", id), ("$cipher", NoteCipher.EncryptText(key, body, BodyContext(id, 1))));
            PutMetadata(db, id, new(title, false, NoteColor.None, null, null, [], ExtractLinks(body).ToArray()));
            Checkpoint(db, id, body, true);
            return Get(db, id)!;
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    });

    private static IEnumerable<string> AttachmentIds(string text) => MarkdownReference.Parse(text)
        .Where(r => r.Target.StartsWith("attachment:", StringComparison.OrdinalIgnoreCase))
        .Select(r => r.Target[11..].Split('#')[0].TrimStart('/'));
    private static string RewriteAttachments(string text, IReadOnlyDictionary<string, string> map)
    {
        foreach (var reference in MarkdownReference.Parse(text).OrderByDescending(r => r.Start))
        {
            if (!reference.Target.StartsWith("attachment:", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = reference.Target[11..].Split('#', 2);
            if (map.TryGetValue(parts[0].TrimStart('/'), out var id))
                text = text.Remove(reference.Start, reference.Length).Insert(reference.Start, reference.ReplaceWith("attachment://" + id + (parts.Length == 2 ? "#" + parts[1] : "")));
        }
        return text;
    }

    private static void RemoveUnusedPublicAssets(SqliteConnection db, IEnumerable<string> candidates)
    {
        var unused = candidates.ToHashSet(StringComparer.Ordinal);
        using (var cmd = Command(db, "SELECT Markdown FROM Notes WHERE Protected=0 UNION ALL SELECT r.Markdown FROM Revisions r JOIN Notes n ON n.Id=r.NoteId WHERE n.Protected=0"))
        using (var r = cmd.ExecuteReader())
            while (r.Read() && unused.Count > 0) unused.ExceptWith(AttachmentIds(r.GetString(0)));
        var removed = 0;
        foreach (var id in unused)
            removed += Execute(db, "DELETE FROM Attachments WHERE Id=$id AND OwnerId IS NULL", ("$id", id));
        if (removed > 0) Execute(db, "UPDATE Protection SET Maintenance=1 WHERE Id=1");
    }

    private string AddProtectedAttachment(SqliteConnection db, string noteId, string name, byte[] bytes) => WithNoteKey(db, noteId, key =>
    {
        // Keyed identifiers deduplicate within a note without exposing public content hashes.
        var digest = HMACSHA256.HashData(key, bytes);
        var id = "p-" + noteId + "-" + Convert.ToHexString(digest).ToLowerInvariant();
        Execute(db, "INSERT OR IGNORE INTO Attachments(Id,Name,Data,OwnerId,CipherName) VALUES($id,'',$data,$owner,$name)",
            ("$id", id), ("$owner", noteId), ("$data", NoteCipher.EncryptPadded(key, bytes, Context(noteId, "asset:" + id))),
            ("$name", NoteCipher.EncryptText(key, AttachmentName(name), Context(noteId, "asset-name:" + id))));
        return id;
    });

    private static string AddPublicAttachment(SqliteConnection db, string name, byte[] bytes)
    {
        var id = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Execute(db, "INSERT OR IGNORE INTO Attachments(Id,Name,Data) VALUES($id,$name,$data)", ("$id", id), ("$name", AttachmentName(name)), ("$data", bytes));
        return id;
    }

    // Names are archive data: both separators count on every platform, unlike Path.GetFileName.
    private static string AttachmentName(string? name)
    {
        name ??= string.Empty;
        return name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
    }

    private (string Name, byte[] Bytes)? ReadAttachment(SqliteConnection db, string id, string? noteId)
    {
        using var cmd = Command(db, "SELECT Name,Data,OwnerId,CipherName FROM Attachments WHERE Id=$id", ("$id", id));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        if (r.IsDBNull(2)) return (r.GetString(0), (byte[])r[1]);
        var owner = r.GetString(2);
        if (noteId is not null && owner != noteId) throw new NoteLockedException();
        return WithNoteKey(db, owner, key => (
            NoteCipher.DecryptText(key, (byte[])r[3], Context(owner, "asset-name:" + id)),
            NoteCipher.Decrypt(key, (byte[])r[1], Context(owner, "asset:" + id))));
    }
}

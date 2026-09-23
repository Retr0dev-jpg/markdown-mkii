using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MarkdownMkII.Core.Text;
using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage;
public sealed partial class NoteDatabase : INoteRepository, INoteResources
{
    private readonly string path;
    private readonly SemaphoreSlim writer = new(1, 1);
    private readonly object initializationLock = new();
    private Task? initialization;
    public string FilePath => path;

    public NoteDatabase(string path)
    {
        this.path = Path.GetFullPath(path);
    }

    public Task InitializeAsync()
    {
        lock (initializationLock)
        {
            if (initialization is null || initialization.IsFaulted || initialization.IsCanceled)
                initialization = InitializeCoreAsync();
            return initialization;
        }
    }

    private SqliteConnection Open()
    {
        // URI mode must also be enabled on the main connection: ATTACH inherits it.
        // Otherwise SQLite treats the shared-memory URI as a different database.
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = new Uri(path).AbsoluteUri, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true, DefaultTimeout = 10, Pooling = false }.ToString());
        db.Open();
        db.CreateFunction("normalize_label", (string value) => Normalize(value));
        db.CreateFunction("mkii_digest", (object? first, object? second) => IntegrityDigest(first as byte[], second as byte[]), isDeterministic: true);
        Execute(db, "PRAGMA synchronous=FULL; PRAGMA secure_delete=ON; PRAGMA temp_store=MEMORY;");
        if (memory is not null) AttachCatalog(db);
        return db;
    }

    private Task InitializeCoreAsync() => Task.Run(() =>
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var db = Open();
        var version = Convert.ToInt32(Scalar(db, "PRAGMA user_version;"));
        if (version > 3)
            throw new InvalidDataException("This archive requires a newer version of Markdown MkII.");
        Execute(db, "PRAGMA journal_mode=WAL;");
        if (version >= 1)
        {
            Execute(db, "CREATE INDEX IF NOT EXISTS NotesTitle ON Notes(Title COLLATE NOCASE) WHERE Trashed=0;");
            MigrateProtection(db);
            return;
        }

        using var transaction = db.BeginTransaction();
        Execute(db, """
            CREATE TABLE Categories(Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Normalized TEXT NOT NULL UNIQUE);
            CREATE TABLE Tags(Id TEXT PRIMARY KEY, Name TEXT NOT NULL, Normalized TEXT NOT NULL UNIQUE);
            CREATE TABLE Notes(Id TEXT PRIMARY KEY, Title TEXT NOT NULL, Markdown TEXT NOT NULL, Preview TEXT NOT NULL,
                Created INTEGER NOT NULL, Modified INTEGER NOT NULL, Version INTEGER NOT NULL DEFAULT 1,
                Favorite INTEGER NOT NULL DEFAULT 0, Color INTEGER NOT NULL DEFAULT 0,
                CategoryId TEXT REFERENCES Categories(Id) ON DELETE SET NULL,
                Archived INTEGER NOT NULL DEFAULT 0, Trashed INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE NoteTags(NoteId TEXT REFERENCES Notes(Id) ON DELETE CASCADE,
                TagId TEXT REFERENCES Tags(Id) ON DELETE CASCADE, PRIMARY KEY(NoteId,TagId));
            CREATE TABLE Revisions(Id INTEGER PRIMARY KEY, NoteId TEXT NOT NULL REFERENCES Notes(Id) ON DELETE CASCADE,
                Markdown TEXT NOT NULL, Created INTEGER NOT NULL);
            CREATE TABLE Attachments(Id TEXT NOT NULL UNIQUE, Name TEXT NOT NULL, Data BLOB NOT NULL);
            CREATE TABLE NoteLinks(NoteId TEXT NOT NULL REFERENCES Notes(Id) ON DELETE CASCADE,
                Target TEXT NOT NULL, Label TEXT NOT NULL, Line INTEGER NOT NULL);
            CREATE TABLE ImportSources(Source TEXT NOT NULL, Hash TEXT NOT NULL, NoteId TEXT REFERENCES Notes(Id) ON DELETE CASCADE,
                PRIMARY KEY(Source,Hash));
            CREATE INDEX NotesTitle ON Notes(Title COLLATE NOCASE) WHERE Trashed=0;
            CREATE INDEX NotesCollection ON Notes(Trashed,Archived,Modified DESC,Id);
            CREATE INDEX NotesFavorites ON Notes(Favorite,Trashed,Archived,Modified DESC);
            CREATE INDEX NotesCategory ON Notes(CategoryId,Trashed,Archived,Modified DESC);
            CREATE INDEX NoteTagsTag ON NoteTags(TagId,NoteId);
            CREATE INDEX RevisionsNote ON Revisions(NoteId,Created DESC);
            CREATE INDEX LinksTarget ON NoteLinks(Target);
            CREATE VIRTUAL TABLE NoteSearch USING fts5(Title,Markdown,content='Notes',content_rowid='rowid',tokenize='unicode61 remove_diacritics 2');
            CREATE TRIGGER SearchInsert AFTER INSERT ON Notes BEGIN
                INSERT INTO NoteSearch(rowid,Title,Markdown) VALUES(new.rowid,new.Title,new.Markdown); END;
            CREATE TRIGGER SearchDelete AFTER DELETE ON Notes BEGIN
                INSERT INTO NoteSearch(NoteSearch,rowid,Title,Markdown) VALUES('delete',old.rowid,old.Title,old.Markdown); END;
            CREATE TRIGGER SearchUpdate AFTER UPDATE OF Title,Markdown ON Notes BEGIN
                INSERT INTO NoteSearch(NoteSearch,rowid,Title,Markdown) VALUES('delete',old.rowid,old.Title,old.Markdown);
                INSERT INTO NoteSearch(rowid,Title,Markdown) VALUES(new.rowid,new.Title,new.Markdown); END;
            PRAGMA user_version=1;
            """);
        transaction.Commit();
        MigrateProtection(db);
    });
    private async Task<T> Read<T>(Func<SqliteConnection, T> action, CancellationToken token = default)
    {
        await InitializeAsync().ConfigureAwait(false);
        await writer.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using var db = Open();
                return action(db);
            }, token).ConfigureAwait(false);
        }
        finally { writer.Release(); }
    }

    private async Task<T> Write<T>(Func<SqliteConnection, T> action, CancellationToken token = default)
    {
        await InitializeAsync().ConfigureAwait(false);
        await writer.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using var db = Open();
                using var tx = db.BeginTransaction();
                var result = action(db);
                token.ThrowIfCancellationRequested();
                Commit(db, tx);
                return result;
            }, token).ConfigureAwait(false);
        }
        finally
        {
            writer.Release();
        }
    }

    internal static SqliteCommand Command(SqliteConnection db, string sql, params (string, object? )[] parameters)
    {
        var command = db.CreateCommand();
        command.CommandText = sql;
        foreach (var(name, value)in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    internal static int Execute(SqliteConnection db, string sql, params (string, object? )[] parameters)
    {
        using var command = Command(db, sql, parameters);
        return command.ExecuteNonQuery();
    }

    internal static object? Scalar(SqliteConnection db, string sql, params (string, object? )[] parameters)
    {
        using var command = Command(db, sql, parameters);
        return command.ExecuteScalar();
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static string Id() => Guid.NewGuid().ToString("N");
    public static string CleanLabel(string label) => Regex.Replace((label ?? "").Trim(), @"\s+", " ");
    private static string Normalize(string label) => CleanLabel(label).Normalize().ToUpperInvariant();
    private static string Preview(string markdown)
    {
        FrontMatter.TrySplit(markdown, out _, out var body);
        var lines = body.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#') && !s.StartsWith("```"));
        var preview = string.Join(" ", lines.Take(12)).Replace('\r', ' ').Trim();
        return preview[..Math.Min(180, preview.Length)];
    }

    private const string SummaryColumns = "n.Id,n.Title,n.Preview,n.Modified,n.Favorite,n.Color,n.CategoryId,c.Name,n.Archived,n.Trashed,n.Protected";
    private NoteSummary Summary(SqliteDataReader r, IReadOnlyList<string>? tags = null) => new(r.GetString(0), r.GetString(1), r.GetString(2), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(3)), r.GetBoolean(4), (NoteColor)r.GetInt32(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), tags ?? [], r.GetBoolean(8), r.GetBoolean(9), r.GetBoolean(10), r.GetBoolean(10) && !IsUnlocked);
    private static string[] Tags(SqliteConnection db, string id)
    {
        using var command = Command(db, "SELECT t.Name FROM CatalogTags t WHERE t.NoteId=$id ORDER BY t.Normalized", ("$id", id));
        using var r = command.ExecuteReader();
        var result = new List<string>();
        while (r.Read())
            result.Add(r.GetString(0));
        return result.ToArray();
    }

    private NoteDocument? Get(SqliteConnection db, string id)
    {
        using var command = Command(db, $"SELECT {SummaryColumns},n.Markdown,n.Version,n.Created FROM Catalog n LEFT JOIN CatalogCategories c ON c.Id=n.CategoryId WHERE n.Id=$id", ("$id", id));
        NoteDocument? result;
        using (var r = command.ExecuteReader())
        {
            if (!r.Read())
                return null;
            result = new(Summary(r), r.GetString(11), r.GetInt64(12), DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(13)));
        }

        var cipher = Scalar(db, "SELECT Cipher FROM Notes WHERE Id=$id", ("$id", id)) as byte[];
        // The Protected column is not authenticated; ciphertext on an "unprotected" row means tampering.
        if (!result.Summary.IsProtected && cipher is not null)
            throw new InvalidDataException("The note has encrypted content but is not marked as protected.");
        if (result.Summary.IsProtected)
        {
            var version = result.Version;
            result = result with { Markdown = WithNoteKey(db, id, key => DecryptBody(key, cipher!, id, version)) };
        }
        return result with
        {
            Summary = result.Summary with
            {
                Tags = Tags(db, id)
            }
        };
    }

    public Task<NoteSummary?> SummaryAsync(string id, CancellationToken token = default) => Read(db =>
    {
        using var command = Command(db, $"SELECT {SummaryColumns} FROM Catalog n LEFT JOIN CatalogCategories c ON c.Id=n.CategoryId WHERE n.Id=$id", ("$id", id));
        NoteSummary? note;
        using (var reader = command.ExecuteReader())
            note = reader.Read() ? Summary(reader) : null;
        return note is null ? null : note with
        {
            Tags = Tags(db, id)
        };
    }, token);
    public Task<NoteDocument?> GetAsync(string id, CancellationToken token = default) => Read(db => Get(db, id), token);
    public Task<NoteDocument> CreateAsync(string title, string markdown = "", CancellationToken token = default) => Write(db =>
    {
        var id = Id();
        var now = Now();
        Execute(db, "INSERT INTO Notes(Id,Title,Markdown,Preview,Created,Modified) VALUES($id,$title,$text,$preview,$now,$now)", ("$id", id), ("$title", CleanLabel(title)), ("$text", markdown), ("$preview", Preview(markdown)), ("$now", now));
        Checkpoint(db, id, markdown, true);
        IndexLinks(db, id, markdown);
        return Get(db, id)!;
    }, token);
    public Task<NoteDocument> SaveAsync(string id, string markdown, long expectedVersion, bool checkpoint = false, CancellationToken token = default)
        => Write(db =>
        {
            var current = Get(db, id) ?? throw new KeyNotFoundException(id);
            if (current.Version != expectedVersion)
                throw new NoteConflictException();
            if (current.Summary.IsProtected) markdown = CanonicalizeProtectedAttachments(db, id, markdown);
            var result = current;
            if (current.Markdown != markdown)
            {
                var now = Now();
                var preview = current.Summary.Preview;
                if (current.Summary.IsProtected)
                {
                    WithNoteKey(db, id, key => Execute(db, "UPDATE Notes SET Cipher=$cipher,Modified=$now,Version=Version+1 WHERE Id=$id",
                        ("$cipher", NoteCipher.EncryptText(key, markdown, BodyContext(id, current.Version + 1))), ("$now", now), ("$id", id)));
                    PutMetadata(db, id, ReadMetadata(db, id) with { Links = ExtractLinks(markdown).ToArray() });
                }
                else
                {
                    preview = Preview(markdown);
                    Execute(db, "UPDATE Notes SET Markdown=$text,Preview=$preview,Modified=$now,Version=Version+1 WHERE Id=$id", ("$text", markdown), ("$preview", preview), ("$now", now), ("$id", id));
                    IndexLinks(db, id, markdown);
                }
                result = current with
                {
                    Markdown = markdown,
                    Version = current.Version + 1,
                    Summary = current.Summary with { Preview = preview, Modified = DateTimeOffset.FromUnixTimeMilliseconds(now) }
                };
            }

            Checkpoint(db, id, markdown, checkpoint);
            return result;
        }, token);
    private void Checkpoint(SqliteConnection db, string id, string markdown, bool force)
    {
        var protectedNote = Protected(db, id);
        using (var command = Command(db, "SELECT Id,Markdown,Created,Cipher FROM Revisions WHERE NoteId=$id ORDER BY Created DESC,Id DESC LIMIT 1", ("$id", id)))
        using (var r = command.ExecuteReader())
        {
            if (r.Read())
            {
                if (!force && Now() - r.GetInt64(2) < 60_000) return;
                var previous = protectedNote ? WithNoteKey(db, id, key => NoteCipher.DecryptText(key, (byte[])r[3], Context(id, "revision:" + r.GetInt64(0)))) : r.GetString(1);
                if (previous == markdown) return;
            }
        }
        Execute(db, "INSERT INTO Revisions(NoteId,Markdown,Created) VALUES($id,$text,$now)", ("$id", id), ("$text", protectedNote ? "" : markdown), ("$now", Now()));
        var revision = Convert.ToInt64(Scalar(db, "SELECT last_insert_rowid()"));
        if (protectedNote) WithNoteKey(db, id, key => Execute(db, "UPDATE Revisions SET Cipher=$cipher WHERE Id=$revision", ("$revision", revision), ("$cipher", NoteCipher.EncryptText(key, markdown, Context(id, "revision:" + revision)))));
        Execute(db, "DELETE FROM Revisions WHERE NoteId=$id AND Id NOT IN (SELECT Id FROM Revisions WHERE NoteId=$id ORDER BY Created DESC,Id DESC LIMIT 30)", ("$id", id));
    }

    public Task<NotePage> QueryAsync(NoteQuery query, CancellationToken token = default) => Read(db =>
    {
        var (where, parameters) = QueryPredicate(query);
        var total = Convert.ToInt32(Scalar(db, $"SELECT count(*) FROM Catalog n WHERE {where}", parameters.ToArray()));
        parameters.Add(("$limit", Math.Clamp(query.Limit, 1, 200)));
        parameters.Add(("$offset", Math.Max(0, query.Offset)));
        using var cmd = Command(db, $"SELECT {SummaryColumns} FROM Catalog n LEFT JOIN CatalogCategories c ON c.Id=n.CategoryId WHERE {where} ORDER BY n.Modified DESC,n.Id LIMIT $limit OFFSET $offset", parameters.ToArray());
        var items = new List<NoteSummary>();
        using (var r = cmd.ExecuteReader())
            while (r.Read())
                items.Add(Summary(r));
        for (var i = 0; i < items.Count; i++)
            items[i] = items[i] with
            {
                Tags = Tags(db, items[i].Id)
            };
        return new NotePage(items, total);
    }, token);
    private static (string Where, List<(string, object?)> Parameters) QueryPredicate(NoteQuery query)
    {
        var parameters = new List<(string, object? )>();
        var where = query.Collection switch
        {
            NoteCollection.Trash => "n.Trashed=1",
            NoteCollection.Archive => "n.Trashed=0 AND n.Archived=1",
            NoteCollection.Favorites => "n.Trashed=0 AND n.Archived=0 AND n.Favorite=1",
            _ => "n.Trashed=0 AND n.Archived=0"
        };
        if (query.CategoryId is not null)
        {
            where += query.CategoryId.Length == 0 ? " AND n.CategoryId IS NULL" : " AND n.CategoryId=$category";
            if (query.CategoryId.Length > 0)
                parameters.Add(("$category", query.CategoryId));
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            where += " AND EXISTS(SELECT 1 FROM CatalogTags t WHERE t.NoteId=n.Id AND t.Normalized=$tag)";
            parameters.Add(("$tag", Normalize(query.Tag.TrimStart('#'))));
        }

        var terms = Regex.Matches(query.Search, @"[\p{L}\p{N}_]+").Select(m => m.Value).Take(20).ToArray();
        if (terms.Length > 0)
        {
            where += " AND (n.rowid IN(SELECT rowid FROM NoteSearch WHERE NoteSearch MATCH $search) OR n.rowid IN(SELECT rowid FROM PublicMetadataSearch WHERE PublicMetadataSearch MATCH $search) OR n.Id IN(SELECT Id FROM session.Search WHERE Search MATCH $search))";
            parameters.Add(("$search", string.Join(" AND ", terms.Select(t => "\"" + t + "\"*"))));
        }

        return (where, parameters);
    }
    public Task<IReadOnlyList<string>> QueryIdsAsync(NoteQuery query, CancellationToken token = default) => Read<IReadOnlyList<string>>(db =>
    {
        var (where, parameters) = QueryPredicate(query);
        using var cmd = Command(db, $"SELECT n.Id FROM Catalog n WHERE {where} ORDER BY n.Modified DESC,n.Id", parameters.ToArray());
        using var r = cmd.ExecuteReader();
        var ids = new List<string>();
        while (r.Read()) { token.ThrowIfCancellationRequested(); ids.Add(r.GetString(0)); }
        return ids;
    }, token);

    public Task<IReadOnlyList<NoteSummary>> SummariesAsync(IEnumerable<string> ids, NoteQuery? query = null, CancellationToken token = default)
    {
        var requested = System.Text.Json.JsonSerializer.Serialize(ids.Distinct().ToArray());
        return Read<IReadOnlyList<NoteSummary>>(db =>
        {
            var (where, parameters) = query is null ? ("1=1", new List<(string, object?)>()) : QueryPredicate(query);
            parameters.Add(("$ids", requested));
            using var cmd = Command(db, $"SELECT {SummaryColumns} FROM json_each($ids) i JOIN Catalog n ON n.Id=i.value LEFT JOIN CatalogCategories c ON c.Id=n.CategoryId WHERE {where} ORDER BY CAST(i.key AS INTEGER)", parameters.ToArray());
            var result = new List<NoteSummary>();
            using (var r = cmd.ExecuteReader()) while (r.Read()) { token.ThrowIfCancellationRequested(); result.Add(Summary(r)); }
            for (var i = 0; i < result.Count; i++) result[i] = result[i] with { Tags = Tags(db, result[i].Id) };
            return result;
        }, token);
    }
    public Task UpdateMetadataAsync(string id, string title, bool favorite, NoteColor color, string? categoryId, IEnumerable<string> tags)
        => Write(db => UpdateMetadata(db, id, title, favorite, color, categoryId, tags));
    private bool UpdateMetadata(SqliteConnection db, string id, string title, bool favorite, NoteColor color, string? categoryId, IEnumerable<string> tags)
    {
        var labels = tags.Select(t => CleanLabel(t.TrimStart('#'))).Where(t => t.Length > 0).DistinctBy(Normalize).ToArray();
            if (Protected(db, id))
            {
                RequireAccess(db, id);
                var category = categoryId is null ? null : Scalar(db, "SELECT Name FROM CatalogCategories WHERE Id=$id", ("$id", categoryId)) as string;
                PutMetadata(db, id, ReadMetadata(db, id) with { Title = CleanLabel(title), Favorite = favorite, Color = color, CategoryId = categoryId, Category = category, Tags = labels });
                Execute(db, "UPDATE Notes SET Modified=$now WHERE Id=$id", ("$now", Now()), ("$id", id));
                return true;
            }
            Execute(db, "UPDATE Notes SET Title=$title,Favorite=$favorite,Color=$color,CategoryId=$category,Modified=$now WHERE Id=$id", ("$title", CleanLabel(title)), ("$favorite", favorite), ("$color", Enum.IsDefined(color) ? (int)color : 0), ("$category", categoryId), ("$now", Now()), ("$id", id));
            Execute(db, "DELETE FROM NoteTags WHERE NoteId=$id", ("$id", id));
            foreach (var tag in labels)
            {
                var tagId = EnsureLabel(db, "Tags", tag);
                Execute(db, "INSERT INTO NoteTags(NoteId,TagId) VALUES($id,$tag)", ("$id", id), ("$tag", tagId));
            }

        return true;
    }

    private static string EnsureLabel(SqliteConnection db, string table, string name)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0)
            throw new ArgumentException("A name is required.", nameof(name));
        var existing = Scalar(db, $"SELECT Id FROM {table} WHERE Normalized=$name", ("$name", normalized)) as string;
        if (existing is not null)
            return existing;
        var id = Id();
        Execute(db, $"INSERT INTO {table}(Id,Name,Normalized) VALUES($id,$name,$normalized)", ("$id", id), ("$name", CleanLabel(name)), ("$normalized", normalized));
        return id;
    }

    public Task<string> AddCategoryAsync(string name) => Write(db => EnsureLabel(db, "Categories", name));
    public Task<string> AddTagAsync(string name) => Write(db => EnsureLabel(db, "Tags", name.TrimStart('#')));
    public Task<IReadOnlyList<NamedLabel>> LabelsAsync(bool categories) => Read<IReadOnlyList<NamedLabel>>(db =>
    {
        var sql = categories ? "SELECT c.Id,c.Name,count(n.Id) FROM CatalogCategories c LEFT JOIN Catalog n ON n.CategoryId=c.Id AND n.Trashed=0 GROUP BY c.Id,c.Name ORDER BY c.Name COLLATE NOCASE" : "SELECT coalesce(t.Id,ct.Name),ct.Name,count(DISTINCT n.Id) FROM (SELECT Name,Normalized FROM CatalogTags UNION SELECT Name,Normalized FROM Tags) ct LEFT JOIN Tags t ON t.Normalized=ct.Normalized LEFT JOIN CatalogTags nt ON nt.Normalized=ct.Normalized LEFT JOIN Notes n ON n.Id=nt.NoteId AND n.Trashed=0 GROUP BY ct.Normalized ORDER BY ct.Normalized";
        using var cmd = Command(db, sql);
        using var r = cmd.ExecuteReader();
        var result = new List<NamedLabel>();
        while (r.Read()) result.Add(new(r.GetString(0), r.GetString(1), r.GetInt32(2)));
        return result;
    });
    public Task<IReadOnlyList<string>> LabelNoteIdsAsync(bool categories, string id) => Read<IReadOnlyList<string>>(db =>
    {
        var name = Scalar(db, "SELECT Name FROM Tags WHERE Id=$id", ("$id", id)) as string ?? id;
        using var cmd = Command(db, categories
            ? "SELECT Id FROM Catalog WHERE CategoryId=$value"
            : "SELECT DISTINCT NoteId FROM CatalogTags WHERE Normalized=$value", ("$value", categories ? id : Normalize(name)));
        using var reader = cmd.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read()) ids.Add(reader.GetString(0));
        return ids;
    });
    public Task RenameLabelAsync(bool categories, string id, string name) => ChangeLabelAsync(categories, id, CleanLabel(name));
    public Task DeleteLabelAsync(bool categories, string id) => ChangeLabelAsync(categories, id, null);
    private Task ChangeLabelAsync(bool categories, string id, string? name) => Write(db =>
    {
        var ids = ProtectedIds(db);
        if (ids.Count > 0) _ = Master;
        var oldName = categories ? Scalar(db, "SELECT Name FROM CatalogCategories WHERE Id=$id", ("$id", id)) as string
            : Scalar(db, "SELECT Name FROM Tags WHERE Id=$id", ("$id", id)) as string ?? id;
        if (oldName is null) return false;
        if (name is { Length: 0 }) throw new ArgumentException("A name is required.");
        // Read encrypted metadata before removing or renaming any public association.
        var values = ids.Select(n => (Id: n, Value: ReadMetadata(db, n))).ToArray();
        var table = categories ? "Categories" : "Tags";
        if (categories) Execute(db, "DELETE FROM session.Categories WHERE Id=$id", ("$id", id));
        if (name is null) Execute(db, $"DELETE FROM {table} WHERE Id=$id", ("$id", id));
        else Execute(db, $"UPDATE {table} SET Name=$name,Normalized=$normal WHERE Id=$id", ("$id", id), ("$name", name), ("$normal", Normalize(name)));
        foreach (var item in values)
        {
            var value = item.Value;
            if (categories && value.CategoryId == id) value = value with { CategoryId = name is null ? null : id, Category = name };
            if (!categories) value = value with { Tags = value.Tags.Select(t => Normalize(t) == Normalize(oldName) ? name : t).OfType<string>().DistinctBy(Normalize).ToArray() };
            if (value != item.Value) PutMetadata(db, item.Id, value);
        }
        return true;
    });
    public Task SetStateAsync(string id, bool archived, bool trashed) => Write(db => Execute(db, "UPDATE Notes SET Archived=$archive,Trashed=$trash WHERE Id=$id", ("$archive", archived), ("$trash", trashed), ("$id", id)));
    public Task DeletePermanentlyAsync(string id) => Write(db =>
    {
        RequireAccess(db, id);
        var changed = Execute(db, "DELETE FROM Notes WHERE Id=$id AND Trashed=1", ("$id", id));
        if (changed > 0) Execute(db, "DELETE FROM session.Metadata WHERE Id=$id; DELETE FROM session.Search WHERE Id=$id; DELETE FROM session.Links WHERE NoteId=$id; DELETE FROM session.Tags WHERE NoteId=$id", ("$id", id));
        return changed;
    });
    public Task<IReadOnlyList<NoteRevision>> RevisionsAsync(string id) => Read<IReadOnlyList<NoteRevision>>(db => ReadRevisions(db, id));
    private List<NoteRevision> ReadRevisions(SqliteConnection db, string id)
    {
        RequireAccess(db, id);
        var protectedNote = Protected(db, id);
        using var cmd = Command(db, "SELECT Id,Markdown,Created,Cipher FROM Revisions WHERE NoteId=$id ORDER BY Created DESC,Id DESC", ("$id", id));
        using var r = cmd.ExecuteReader();
        var result = new List<NoteRevision>();
        while (r.Read())
        {
            var revision = r.GetInt64(0);
            var text = protectedNote ? WithNoteKey(db, id, key => NoteCipher.DecryptText(key, (byte[])r[3], Context(id, "revision:" + revision))) : r.GetString(1);
            result.Add(new(revision, text, DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(2))));
        }
        return result;
    }
    public Task<ArchiveStats> StatsAsync() => Read(db => new ArchiveStats(Convert.ToInt32(Scalar(db, "SELECT count(*) FROM Notes WHERE Trashed=0")), Convert.ToInt32(Scalar(db, "SELECT count(*) FROM Notes WHERE Favorite=1 AND Trashed=0")), Convert.ToInt32(Scalar(db, "SELECT count(*) FROM Tags")), Convert.ToInt32(Scalar(db, "SELECT count(*) FROM Categories")), Convert.ToInt64(Scalar(db, "SELECT coalesce(sum(length(Data)),0) FROM Attachments"))));
}

using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage;

public enum BulkAction { Favorite, Unfavorite, Category, AddTags, RemoveTags, Color, Archive, Unarchive, Trash, Restore, Delete, Duplicate, Protect, Unprotect, Export }
public sealed record BulkRequest(BulkAction Action, string? CategoryId = null, IReadOnlyList<string>? Tags = null, NoteColor Color = NoteColor.None, string CopySuffix = "copy");
public sealed record BulkProgress(int Completed, int Unchanged, int Failed, int Total);
public sealed record BulkResult(IReadOnlyList<string> ChangedIds, IReadOnlyList<string> CreatedIds, IReadOnlyList<string> FailedIds, int Unchanged, int NotExecuted, bool MaintenancePending);

public sealed partial class NoteDatabase
{
    public static bool BulkApplies(NoteSummary note, BulkRequest request) => request.Action switch
    {
        BulkAction.Restore or BulkAction.Delete => note.Trashed,
        _ when note.Trashed => false,
        BulkAction.Favorite => !note.Favorite,
        BulkAction.Unfavorite => note.Favorite,
        BulkAction.Archive => !note.Archived,
        BulkAction.Unarchive => note.Archived,
        BulkAction.Protect => !note.IsProtected,
        BulkAction.Unprotect => note.IsProtected,
        BulkAction.Category => note.CategoryId != request.CategoryId,
        BulkAction.Color => note.Color != request.Color,
        BulkAction.AddTags => (request.Tags ?? []).Any(t => !note.Tags.Any(n => Normalize(n) == Normalize(t))),
        BulkAction.RemoveTags => (request.Tags ?? []).Any(t => note.Tags.Any(n => Normalize(n) == Normalize(t))),
        _ => true
    };

    public async Task<BulkResult> ApplyBulkAsync(IReadOnlyList<string> ids, BulkRequest request, IProgress<BulkProgress>? progress = null, CancellationToken token = default)
    {
        var targets = ids.Distinct().ToArray();
        var changed = new List<string>(); var created = new List<string>(); var failed = new List<string>();
        var unchanged = 0; var executed = 0;
        var security = request.Action is BulkAction.Protect or BulkAction.Unprotect;
        foreach (var chunk in targets.Chunk(50))
        {
            if (token.IsCancellationRequested) break;
            // Each chunk commits independently. Cancellation stops before the next note;
            // committed changes are reported rather than silently rolled back later.
            var chunkChanged = new List<string>(); var chunkCreated = new List<string>(); var chunkFailed = new List<string>();
            var chunkUnchanged = 0; var chunkExecuted = 0;
            try
            {
            await Write(db =>
            {
                foreach (var id in chunk)
                {
                    if (token.IsCancellationRequested) break;
                    Execute(db, "SAVEPOINT bulk_note");
                    try
                    {
                        var note = SummaryOnConnection(db, id);
                        if (note is null || !BulkApplies(note, request)) chunkUnchanged++;
                        else
                        {
                            RequireAccess(db, id);
                            string? newId = null;
                            switch (request.Action)
                            {
                                case BulkAction.Protect: Protect(db, id); break;
                                case BulkAction.Unprotect: Unprotect(db, id); break;
                                case BulkAction.Duplicate: newId = Duplicate(db, id, note.Title + " " + request.CopySuffix).Summary.Id; break;
                                case BulkAction.Archive: case BulkAction.Unarchive:
                                    Execute(db, "UPDATE Notes SET Archived=$value WHERE Id=$id", ("$value", request.Action == BulkAction.Archive), ("$id", id)); break;
                                case BulkAction.Trash: case BulkAction.Restore:
                                    Execute(db, "UPDATE Notes SET Trashed=$value WHERE Id=$id", ("$value", request.Action == BulkAction.Trash), ("$id", id)); break;
                                case BulkAction.Delete:
                                    Execute(db, "DELETE FROM Notes WHERE Id=$id AND Trashed=1; DELETE FROM session.Metadata WHERE Id=$id; DELETE FROM session.Search WHERE Id=$id; DELETE FROM session.Links WHERE NoteId=$id; DELETE FROM session.Tags WHERE NoteId=$id", ("$id", id)); break;
                                case BulkAction.Export: throw new InvalidOperationException("Export uses the transfer service.");
                                default:
                                    var tags = request.Action switch
                                    {
                                        BulkAction.AddTags => note.Tags.Concat(request.Tags ?? []).DistinctBy(Normalize),
                                        BulkAction.RemoveTags => note.Tags.Where(t => !(request.Tags ?? []).Any(x => Normalize(x) == Normalize(t))),
                                        _ => note.Tags
                                    };
                                    UpdateMetadata(db, id, note.Title,
                                        request.Action == BulkAction.Favorite || request.Action != BulkAction.Unfavorite && note.Favorite,
                                        request.Action == BulkAction.Color ? request.Color : note.Color,
                                        request.Action == BulkAction.Category ? request.CategoryId : note.CategoryId, tags);
                                    break;
                            }
                            if (security) Execute(db, "UPDATE Protection SET Maintenance=1 WHERE Id=1");
                            if (newId is not null) chunkCreated.Add(newId); else chunkChanged.Add(id);
                        }
                        Execute(db, "RELEASE bulk_note");
                    }
                    catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or System.Security.Cryptography.CryptographicException or KeyNotFoundException)
                    {
                        Execute(db, "ROLLBACK TO bulk_note; RELEASE bulk_note");
                        if (IsUnlocked) RebuildPrivateCatalog(db);
                        chunkFailed.Add(id);
                    }
                    chunkExecuted++;
                }
                return true;
            });
            }
            catch (Exception ex) when (ex is SqliteException or IOException or InvalidOperationException or System.Security.Cryptography.CryptographicException)
            {
                // A transaction failure rolls back the entire chunk, including successful
                // savepoints. Never report those tentative results as committed changes.
                failed.AddRange(chunk); executed += chunk.Length;
                break;
            }
            changed.AddRange(chunkChanged); created.AddRange(chunkCreated); failed.AddRange(chunkFailed);
            unchanged += chunkUnchanged; executed += chunkExecuted;
            progress?.Report(new(changed.Count + created.Count, unchanged, failed.Count, targets.Length));
        }
        var maintenancePending = false;
        if (security && changed.Count > 0)
        {
            try { await CompleteProtectionMaintenanceAsync(); }
            catch (Exception ex) when (ex is IOException or SqliteException) { maintenancePending = true; }
        }
        return new(changed, created, failed, unchanged, targets.Length - executed, maintenancePending);
    }

    private NoteSummary? SummaryOnConnection(SqliteConnection db, string id)
    {
        using var cmd = Command(db, $"SELECT {SummaryColumns} FROM Catalog n LEFT JOIN CatalogCategories c ON c.Id=n.CategoryId WHERE n.Id=$id", ("$id", id));
        NoteSummary? result;
        using (var r = cmd.ExecuteReader()) result = r.Read() ? Summary(r) : null;
        return result is null ? null : result with { Tags = Tags(db, id) };
    }
}

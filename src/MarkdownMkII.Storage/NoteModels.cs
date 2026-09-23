namespace MarkdownMkII.Storage;
public enum NoteCollection
{
    All,
    Favorites,
    Archive,
    Trash
}

public enum NoteColor
{
    None,
    Red,
    Orange,
    Yellow,
    Green,
    Teal,
    Blue,
    Purple,
    Pink
}

public sealed record NoteSummary(string Id, string Title, string Preview, DateTimeOffset Modified, bool Favorite, NoteColor Color, string? CategoryId, string? Category, IReadOnlyList<string> Tags, bool Archived, bool Trashed, bool IsProtected = false, bool IsLocked = false);
public sealed record NoteDocument(NoteSummary Summary, string Markdown, long Version, DateTimeOffset Created);
public sealed record NoteQuery(string Search = "", NoteCollection Collection = NoteCollection.All, string? CategoryId = null, string? Tag = null, int Offset = 0, int Limit = 100);
public sealed record NotePage(IReadOnlyList<NoteSummary> Items, int Total);
public sealed record NamedLabel(string Id, string Name, int Count);
public sealed record NoteRevision(long Id, string Markdown, DateTimeOffset Created);
public sealed record NoteLink(string Target, string Label, int Line);
public sealed record ArchiveStats(int Notes, int Favorites, int Tags, int Categories, long Attachments);
public sealed class NoteConflictException() : Exception("The note changed after it was loaded.");
public interface INoteRepository
{
    Task InitializeAsync();
    Task<NoteDocument> CreateAsync(string title, string markdown = "", CancellationToken token = default);
    Task<NoteSummary?> SummaryAsync(string id, CancellationToken token = default);
    Task<NoteDocument?> GetAsync(string id, CancellationToken token = default);
    Task<NoteDocument> SaveAsync(string id, string markdown, long expectedVersion, bool checkpoint = false, CancellationToken token = default);
    Task<NotePage> QueryAsync(NoteQuery query, CancellationToken token = default);
    Task<IReadOnlyList<string>> QueryIdsAsync(NoteQuery query, CancellationToken token = default);
    Task<IReadOnlyList<NoteSummary>> SummariesAsync(IEnumerable<string> ids, NoteQuery? query = null, CancellationToken token = default);
    Task<BulkResult> ApplyBulkAsync(IReadOnlyList<string> ids, BulkRequest request, IProgress<BulkProgress>? progress = null, CancellationToken token = default);
    Task UpdateMetadataAsync(string id, string title, bool favorite, NoteColor color, string? categoryId, IEnumerable<string> tags);
    Task SetStateAsync(string id, bool archived, bool trashed);
    Task DeletePermanentlyAsync(string id);
    Task<IReadOnlyList<NoteRevision>> RevisionsAsync(string id);
}

public interface INoteResources
{
    Task<IReadOnlyList<NoteSummary>> ResolveAsync(string target, CancellationToken token = default);
    Task<string> AddAttachmentAsync(Stream input, string name, CancellationToken token = default, string? noteId = null);
    Task CopyAttachmentAsync(string id, Stream destination, CancellationToken token = default, string? noteId = null);
    Task<string?> AttachmentNameAsync(string id, string? noteId = null);
}

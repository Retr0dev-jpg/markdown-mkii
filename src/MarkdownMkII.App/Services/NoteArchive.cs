using MarkdownMkII.Storage;
namespace MarkdownMkII.Services;
public enum ArchiveChangeKind { Content, Metadata, Inserted, Removed, Organization, Reset }
public sealed class ArchiveChangedEventArgs(ArchiveChangeKind kind, IReadOnlyList<string> ids, IReadOnlyList<NoteSummary>? summaries = null) : EventArgs
{
    public ArchiveChangeKind Kind { get; } = kind;
    public IReadOnlyList<string> Ids { get; } = ids;
    public IReadOnlyList<NoteSummary> Summaries { get; } = summaries ?? [];
    public bool LabelsChanged => Kind is ArchiveChangeKind.Metadata or ArchiveChangeKind.Organization or ArchiveChangeKind.Inserted or ArchiveChangeKind.Removed or ArchiveChangeKind.Reset;
}
public static class NoteArchive
{
    public static NoteDatabase Database { get; } = new(Path.Combine(AppPaths.Root, "notes.db")) { Anchor = new Interop.IntegrityAnchorStore() };
    public static MarkdownTransfer Transfer { get; } = new(Database);
    public static event EventHandler<ArchiveChangedEventArgs>? Changed;
    public static void NotifyChanged() => Changed?.Invoke(null, new(ArchiveChangeKind.Reset, []));
    public static void NotifyChanged(ArchiveChangeKind kind, IEnumerable<string> ids, IReadOnlyList<NoteSummary>? summaries = null)
        => Changed?.Invoke(null, new(kind, ids.Distinct().ToArray(), summaries));
    public static void NotifyChanged(ArchiveChangeKind kind, NoteSummary summary)
        => NotifyChanged(kind, [summary.Id], [summary]);
}

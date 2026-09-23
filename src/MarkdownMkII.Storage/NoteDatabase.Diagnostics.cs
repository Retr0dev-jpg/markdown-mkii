namespace MarkdownMkII.Storage;

/// <summary>Technical state of the archive for bug reports: counts and flags only, never note text or titles.</summary>
public sealed record ArchiveDiagnostics(
    int SchemaVersion, string SqliteVersion, string JournalMode, long DatabaseBytes, long WalBytes,
    int Notes, int ProtectedNotes, int ArchivedNotes, int TrashedNotes, int Revisions, int Attachments, long AttachmentBytes,
    bool ProtectionConfigured, bool Unlocked, bool HideDetails, bool MaintenancePending, bool IntegrityIssue);

public sealed partial class NoteDatabase
{
    public Task<ArchiveDiagnostics> DiagnosticsAsync() => Read(db =>
    {
        long Count(string sql) => Convert.ToInt64(Scalar(db, sql));
        static long Size(string file) => File.Exists(file) ? new FileInfo(file).Length : 0;
        return new ArchiveDiagnostics(
            (int)Count("PRAGMA user_version"),
            (string)Scalar(db, "SELECT sqlite_version()")!,
            (string)Scalar(db, "PRAGMA journal_mode")!,
            Size(path), Size(path + "-wal"),
            (int)Count("SELECT count(*) FROM Notes WHERE Trashed=0"),
            (int)Count("SELECT count(*) FROM Notes WHERE Protected=1"),
            (int)Count("SELECT count(*) FROM Notes WHERE Archived=1 AND Trashed=0"),
            (int)Count("SELECT count(*) FROM Notes WHERE Trashed=1"),
            (int)Count("SELECT count(*) FROM Revisions"),
            (int)Count("SELECT count(*) FROM Attachments"),
            Count("SELECT coalesce(sum(length(Data)),0) FROM Attachments"),
            IsProtectionConfigured, IsUnlocked, hideDetails,
            Count("SELECT Maintenance FROM Protection WHERE Id=1") != 0,
            IntegrityReport is not null);
    });
}

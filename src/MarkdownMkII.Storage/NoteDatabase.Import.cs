namespace MarkdownMkII.Storage;
public sealed partial class NoteDatabase
{
    internal Task<NoteDocument?> FindImportAsync(string path, string hash) => Read(db =>
    {
        var id = Scalar(db, "SELECT NoteId FROM ImportSources WHERE Source=$source AND Hash=$hash", ("$source", Path.GetFullPath(path).ToUpperInvariant()), ("$hash", hash)) as string;
        return id is null ? null : Get(db, id);
    });
    internal Task RememberImportAsync(string path, string hash, string id) => Write(db => Execute(db, "INSERT OR REPLACE INTO ImportSources(Source,Hash,NoteId) VALUES($source,$hash,$id)", ("$source", Path.GetFullPath(path).ToUpperInvariant()), ("$hash", hash), ("$id", id)));
}

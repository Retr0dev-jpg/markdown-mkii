using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Storage;
public sealed record ImportCandidate(string Path, string Title, long Size);
public sealed record ImportResult(IReadOnlyList<string> NoteIds, IReadOnlyList<string> Warnings);
public sealed class MarkdownTransfer(NoteDatabase database)
{
    public static Task<IReadOnlyList<ImportCandidate>> InspectAsync(IEnumerable<string> paths, CancellationToken token = default) => Task.Run<IReadOnlyList<ImportCandidate>>(() =>
    {
        var result = new List<ImportCandidate>();
        foreach (var path in paths.SelectMany(p => Directory.Exists(p) ? WorkspaceScanner.EnumerateMarkdownFiles(p) : [p]).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            if (!DocumentStore.IsMarkdown(path) || !File.Exists(path))
                continue;
            result.Add(new(Path.GetFullPath(path), Path.GetFileNameWithoutExtension(path), new FileInfo(path).Length));
        }

        return result;
    }, token);
    public Task<ImportResult> ImportAsync(IEnumerable<ImportCandidate> selection, CancellationToken token = default, bool includeAttachments = true)
    {
        var items = selection.ToArray();
        return Task.Run(() => ImportCoreAsync(items, token, includeAttachments), token);
    }

    private async Task<ImportResult> ImportCoreAsync(IEnumerable<ImportCandidate> selection, CancellationToken token, bool includeAttachments)
    {
        var notes = new List<(string Path, string Id)>();
        var warnings = new List<string>();
        foreach (var item in selection.DistinctBy(i => Path.GetFullPath(i.Path), StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var snapshot = await new DocumentStore().LoadAsync(item.Path, token);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.Text)));
                var previous = await database.FindImportAsync(item.Path, hash);
                if (previous is not null)
                {
                    notes.Add((item.Path, previous.Summary.Id));
                    continue;
                }

                var metadata = ImportMetadata.Read(snapshot.Text);
                var fields = metadata.Fields;
                if (metadata.Warning is not null)
                    warnings.Add(item.Title + ": " + metadata.Warning);
                var title = fields.GetValueOrDefault("title");
                if (string.IsNullOrWhiteSpace(title))
                    title = WikiIndex.NoteTitle(item.Path, snapshot.Text);
                var category = fields.GetValueOrDefault("category");
                var note = await database.CreateAsync(title ?? item.Title, snapshot.Text, token);
                var categoryId = string.IsNullOrWhiteSpace(category) ? null : await database.AddCategoryAsync(category);
                var favorite = fields.GetValueOrDefault("mkii_favorite") == "true";
                Enum.TryParse<NoteColor>(fields.GetValueOrDefault("mkii_color"), true, out var color);
                await database.UpdateMetadataAsync(note.Summary.Id, note.Summary.Title, favorite, color, categoryId, metadata.Tags);
                await database.RememberImportAsync(item.Path, hash, note.Summary.Id);
                notes.Add((item.Path, note.Summary.Id));
            }
            catch (Exception ex)when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add(Path.GetFileName(item.Path) + ": " + ex.Message);
            }
        }

        var paths = notes.ToDictionary(n => Path.GetFullPath(n.Path), n => n.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var(path, noteId)in notes)
        {
            token.ThrowIfCancellationRequested();
            var note = await database.GetAsync(noteId, token);
            if (note is null)
                continue;
            var markdown = note.Markdown;
            var replacements = new List<(int Start, int Length, string Text)>();
            foreach (var link in MarkdownReference.Parse(markdown))
            {
                var url = link.Target;
                if (url.Length == 0 || url.StartsWith('#') || Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme != "file")
                    continue;
                var parts = url.Split('#', 2);
                string full;
                try
                {
                    full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, Uri.UnescapeDataString(parts[0])));
                }
                catch (ArgumentException)
                {
                    continue;
                }

                string? target = null;
                var notePath = link.Wiki && !link.Image && !DocumentStore.IsMarkdown(full) ? full + ".md" : full;
                if (!link.Image && paths.TryGetValue(notePath, out var targetId))
                    target = "note://" + targetId + (parts.Length > 1 ? "#" + parts[1] : "");
                else if (link.Wiki && !link.Image)
                {
                    var matches = await database.ResolveAsync(parts[0], token);
                    if (matches.Count == 1)
                        target = "note://" + matches[0].Id + (parts.Length > 1 ? "#" + parts[1] : "");
                }

                if (target is null && !DocumentStore.IsMarkdown(full) && File.Exists(full))
                {
                    if (includeAttachments)
                    {
                        try
                        {
                            using var input = File.OpenRead(full);
                            var asset = await database.AddAttachmentAsync(input, Path.GetFileName(full), token, noteId);
                            target = "attachment://" + asset;
                        }
                        catch (Exception ex)when (ex is IOException or UnauthorizedAccessException)
                        {
                            warnings.Add(Path.GetFileName(full) + ": " + ex.Message);
                        }
                    }
                }

                if (target is null)
                    warnings.Add(Path.GetFileName(path) + ": " + url);
                else
                    replacements.Add((link.Start, link.Length, link.ReplaceWith(target)));
            }

            foreach (var replacement in replacements.OrderByDescending(r => r.Start))
                markdown = markdown.Remove(replacement.Start, replacement.Length).Insert(replacement.Start, replacement.Text);
            if (markdown != note.Markdown)
                await database.SaveAsync(note.Summary.Id, markdown, note.Version, true, token);
            foreach (var link in NoteDatabase.ExtractLinks(markdown).Where(l => !l.Target.Contains(':') && !l.Target.StartsWith('#')))
                if ((await database.ResolveAsync(link.Target, token)).Count != 1)
                    warnings.Add(note.Summary.Title + ": [[" + link.Target + "]]");
        }

        return new(notes.Select(n => n.Id).ToArray(), warnings.Distinct().ToArray());
    }

    public async Task ExportAsync(IEnumerable<string> ids, string folder, CancellationToken token = default)
    {
        var selected = ids.Distinct().ToArray();
        foreach (var note in await database.SummariesAsync(selected, token: token))
            if (note.IsLocked) throw new NoteLockedException();
        var result = await ExportDetailedAsync(selected, folder, token: token);
        token.ThrowIfCancellationRequested();
        if (result.FailedIds.Count > 0) throw new IOException("Some notes could not be exported.");
    }
    public Task<BulkResult> ExportDetailedAsync(IEnumerable<string> ids, string folder, IProgress<BulkProgress>? progress = null, CancellationToken token = default)
    {
        var selection = ids.Distinct().ToArray();
        return Task.Run(() => ExportCoreAsync(selection, folder, progress, token));
    }
    private async Task<BulkResult> ExportCoreAsync(IReadOnlyList<string> ids, string folder, IProgress<BulkProgress>? progress, CancellationToken token)
    {
        var completed = new List<string>(); var failed = new List<string>();
        // A lock changes the session: text already decrypted must not be written after it.
        var session = database.SessionVersion;
        bool Locked() => database.SessionVersion != session || token.IsCancellationRequested;
        Directory.CreateDirectory(folder);
        var notes = new List<NoteSummary>();
        foreach (var id in ids)
        {
            if (token.IsCancellationRequested) return new(completed, [], failed, 0, ids.Count, false);
            var n = await database.SummaryAsync(id, token);
            if (n is not null)
                notes.Add(n);
        }

        var paths = new Dictionary<string, string>();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var note in notes)
        {
            var path = DocumentStore.UniqueMarkdownPath(folder, note.Title);
            var suffix = 2;
            while (!reserved.Add(path))
                path = DocumentStore.UniqueMarkdownPath(folder, note.Title + "-" + suffix++);
            paths[note.Id] = path;
        }

        foreach (var summary in notes)
        {
            if (token.IsCancellationRequested) break;
            try
            {
            var note = await database.GetAsync(summary.Id, token);
            if (Locked()) break;
            if (note is null)
                continue;
            var text = note.Markdown;
            foreach (var link in MarkdownReference.Parse(text).OrderByDescending(r => r.Start))
            {
                var parts = link.Target.Split('#', 2);
                var fragment = parts.Length > 1 ? "#" + parts[1] : "";
                string? target = null;
                if (parts[0].StartsWith("attachment:", StringComparison.OrdinalIgnoreCase))
                {
                    var id = parts[0][11..].TrimStart('/');
                    var name = await database.AttachmentNameAsync(id, note.Summary.Id);
                    if (name is null)
                        continue;
                    var relative = "attachments/" + id + Path.GetExtension(name);
                    var destination = Path.Combine(folder, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    if (!File.Exists(destination))
                    {
                        try
                        {
                            await using var output = File.Create(destination);
                            await database.CopyAttachmentAsync(id, output, token, note.Summary.Id);
                        }
                        catch
                        {
                            File.Delete(destination);
                            throw;
                        }

                        if (Locked()) { File.Delete(destination); throw new OperationCanceledException(token); }
                    }

                    target = relative + fragment;
                }
                else if (parts[0].StartsWith("note:", StringComparison.OrdinalIgnoreCase) && paths.TryGetValue(parts[0][5..].TrimStart('/'), out var path))
                    target = Uri.EscapeDataString(Path.GetFileName(path)) + fragment;
                if (target is not null)
                    text = text.Remove(link.Start, link.Length).Insert(link.Start, link.ReplaceWith(target));
            }

            text = ExportMetadata.Apply(text, note.Summary);
            if (Locked()) break;
            await new DocumentStore().SaveAsync(paths[note.Summary.Id], text, TextFileProfile.Utf8Lf, token);
            completed.Add(summary.Id);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or System.Security.Cryptography.CryptographicException or NoteLockedException)
            { failed.Add(summary.Id); }
            progress?.Report(new(completed.Count, 0, failed.Count, ids.Count));
        }
        return new(completed, [], failed, ids.Count - notes.Count, notes.Count - completed.Count - failed.Count, false);
    }
}

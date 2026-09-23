using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Services;

public sealed record LibraryNote(string Path, string Title, string Description, IReadOnlyList<string> Tags,
    DateTimeOffset Modified, string Folder, bool IsArchived, bool IsInLibrary);

public sealed record LibraryFolder(string Path, string Label);

public sealed record LibrarySnapshot(IReadOnlyList<LibraryNote> Notes, IReadOnlyList<LibraryFolder> Folders)
{
    public static LibrarySnapshot Empty { get; } = new([], []);
}

public enum LibraryCollection { All, Recent, Starred, Archive }
public enum LibrarySort { Modified, Title, Oldest }

public static class LibraryCatalog
{
    public static LibrarySnapshot Scan(IEnumerable<string> folders, IEnumerable<string>? additionalPaths = null,
        CancellationToken cancellationToken = default)
    {
        var roots = folders.Where(Directory.Exists).Select(System.IO.Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Length).ToArray();
        var notes = new Dictionary<string, LibraryNote>(StringComparer.OrdinalIgnoreCase);
        var directories = new Dictionary<string, LibraryFolder>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Visit(WorkspaceScanner.Scan(root, cancellationToken: cancellationToken), root);
        }

        foreach (var path in additionalPaths ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path) || !DocumentStore.IsMarkdown(path)) continue;
            var full = System.IO.Path.GetFullPath(path);
            if (notes.ContainsKey(full)) continue;
            var root = roots.FirstOrDefault(r => IsWithin(full, r));
            notes.Add(full, ReadNote(full, roots, root));
        }
        return new(notes.Values.ToArray(), directories.Values.ToArray());

        void Visit(WorkspaceEntry entry, string root)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.IsDirectory)
            {
                if (!directories.TryAdd(entry.Path, new(entry.Path, FolderLabel(entry.Path, root)))) return;
                foreach (var child in entry.Children) Visit(child, root);
            }
            else if (!notes.ContainsKey(entry.Path))
            {
                notes.Add(entry.Path, ReadNote(entry.Path, roots, root));
            }
        }
    }

    private static LibraryNote ReadNote(string path, string[] roots, string? root)
    {
        string text = string.Empty;
        try
        {
            // Cards need only a short sample, not a complete parse of a potentially large document.
            using var reader = new StreamReader(path);
            var buffer = new char[65536];
            var length = reader.ReadBlock(buffer, 0, buffer.Length);
            text = new string(buffer, 0, length);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        FrontMatter.TrySplit(text, out var fields, out var body);
        var description = fields.GetValueOrDefault("description");
        if (string.IsNullOrWhiteSpace(description))
        {
            var lines = body.Split('\n');
            var inFence = false;
            var paragraphs = new List<string>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~")) { inFence = !inFence; continue; }
                if (inFence || trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed == "---") continue;
                paragraphs.Add(trimmed.TrimStart('>', '-', '*', ' '));
                if (string.Join(' ', paragraphs).Length >= 180) break;
            }
            description = string.Join(' ', paragraphs);
        }
        description = description.Replace('\r', ' ').Replace('\n', ' ');
        if (description.Length > 200) description = description[..197].TrimEnd() + "…";
        var parent = System.IO.Path.GetDirectoryName(path)!;
        return new(path, WikiIndex.NoteTitle(path, text), description, MarkdownTags.Extract(text, fields),
            File.GetLastWriteTimeUtc(path), FolderLabel(parent, root ?? parent),
            roots.Any(r => VaultAttachments.IsArchived(path, [r])), root is not null);
    }

    private static string FolderLabel(string path, string root)
    {
        var name = System.IO.Path.GetFileName(root.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(name)) name = root;
        var relative = System.IO.Path.GetRelativePath(root, path);
        return relative == "." ? name : name + " / " + relative.Replace('\\', '/');
    }

    public static bool IsWithin(string path, string folder)
    {
        var prefix = System.IO.Path.GetFullPath(folder).TrimEnd(System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        return System.IO.Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<LibraryNote> Select(LibrarySnapshot catalog, LibraryCollection collection,
        IReadOnlyDictionary<string, DateTimeOffset> recents, IReadOnlySet<string> starred,
        string? folder = null, LibrarySort sort = LibrarySort.Modified)
    {
        IEnumerable<LibraryNote> notes = catalog.Notes.Where(n => collection switch
        {
            LibraryCollection.Recent => recents.ContainsKey(n.Path) && !n.IsArchived,
            LibraryCollection.Starred => starred.Contains(n.Path) && !n.IsArchived,
            LibraryCollection.Archive => n.IsArchived,
            _ => n.IsInLibrary && !n.IsArchived
        });
        if (folder is not null) notes = notes.Where(n => IsWithin(n.Path, folder));
        var ordered = sort switch
        {
            LibrarySort.Title => notes.OrderBy(n => n.Title, StringComparer.CurrentCultureIgnoreCase),
            LibrarySort.Oldest => notes.OrderBy(n => n.Modified),
            _ when collection == LibraryCollection.Recent => notes.OrderByDescending(n => recents[n.Path]),
            _ => notes.OrderByDescending(n => n.Modified)
        };
        return ordered.ThenBy(n => n.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<WikiBacklink> Search(IReadOnlyList<LibraryNote> notes, string query,
        CancellationToken cancellationToken = default)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results = new List<WikiBacklink>();
        bool Matches(string value) => terms.All(t => value.Contains(t, StringComparison.OrdinalIgnoreCase));
        foreach (var note in notes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Matches(note.Title + " " + System.IO.Path.GetFileName(note.Path) + " " + note.Description + " " + string.Join(' ', note.Tags)))
            {
                results.Add(new(note.Path, note.Title, 1, note.Description));
                continue;
            }
            try
            {
                using var reader = new StreamReader(note.Path);
                var lineNumber = 0;
                while (reader.ReadLine() is { } line)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    lineNumber++;
                    if (!Matches(line)) continue;
                    results.Add(new(note.Path, note.Title, lineNumber, line.Length > 200 ? line[..200] + "…" : line));
                    break;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return results;
    }
}

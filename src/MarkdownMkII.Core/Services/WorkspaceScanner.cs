namespace MarkdownMkII.Core.Services;

public sealed record WorkspaceEntry(
    string Path,
    string Name,
    bool IsDirectory,
    DateTimeOffset Modified,
    IReadOnlyList<WorkspaceEntry> Children);

public static class WorkspaceScanner
{
    public static WorkspaceEntry Scan(string folderPath, int maxDepth = 6, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        var info = new DirectoryInfo(folderPath);
        if (!info.Exists)
        {
            throw new DirectoryNotFoundException(folderPath);
        }

        return ScanDirectory(info, 0, maxDepth, cancellationToken);
    }

    public static WorkspaceEntry Filter(WorkspaceEntry root, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return root;
        }

        var needle = query.Trim();
        return root with { Children = FilterChildren(root.Children, needle) };
    }

    public static IReadOnlyList<string> EnumerateMarkdownFiles(
        string folderPath, bool recursive = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        var paths = new List<string>();
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach (var path in Directory.EnumerateFiles(folderPath, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DocumentStore.IsMarkdown(path))
                {
                    paths.Add(Path.GetFullPath(path));
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    internal static IEnumerable<string> EnumerateMarkdownPaths(IEnumerable<string> folders)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            foreach (var path in EnumerateMarkdownFiles(folder))
            {
                if (seen.Add(path))
                {
                    yield return path;
                }
            }
        }
    }

    private static IEnumerable<(string Path, string Text)> ReadMarkdown(IEnumerable<string> folders)
    {
        foreach (var path in EnumerateMarkdownPaths(folders))
        {
            if (TryReadText(path, out var text))
            {
                yield return (path, text);
            }
        }
    }

    internal static bool TryReadText(string path, out string text)
    {
        try
        {
            text = Text.TextFileCodec.Decode(File.ReadAllBytes(path)).Text;
            return true;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        text = string.Empty;
        return false;
    }

    public sealed record SearchHit(string Path, string Name, int Line, string Preview);

    public static IReadOnlyList<SearchHit> SearchContent(IEnumerable<string> folders, string query, int maxHits = 80)
    {
        var terms = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
        {
            return [];
        }

        return SearchLines(folders, maxHits, (text, map) =>
            terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))
                ? Enumerable.Range(0, map.LineCount).Where(line =>
                    terms.Any(term => map.LineText(line).Contains(term, StringComparison.OrdinalIgnoreCase)))
                : []);
    }

    public static IReadOnlyList<SearchHit> SearchRegex(IEnumerable<string> folders, string pattern, int maxHits = 80)
    {
        pattern = (pattern ?? string.Empty).Trim();
        if (pattern.Length == 0)
        {
            return [];
        }

        var options = new Text.FindReplaceOptions { UseRegex = true };
        return SearchLines(folders, maxHits, (text, map) =>
            Text.FindReplace.FindAll(text, pattern, options).Select(match => map.LineOfOffset(match.Start)).Distinct());
    }

    private static IReadOnlyList<SearchHit> SearchLines(
        IEnumerable<string> folders, int maxHits, Func<string, Text.LineMap, IEnumerable<int>> matchingLines)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<SearchHit>();
        foreach (var (path, text) in ReadMarkdown(folders))
        {
            var map = new Text.LineMap(text);
            foreach (var line in matchingLines(text, map).Take(8))
            {
                hits.Add(new SearchHit(path, Path.GetFileName(path), line + 1, map.LineText(line).Trim()));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static int ReplaceContent(
        IEnumerable<string> folders,
        string query,
        string replacement,
        Text.FindReplaceOptions? options = null,
        IEnumerable<string>? skipPaths = null)
    {
        query ??= string.Empty;
        if (query.Length == 0)
        {
            return 0;
        }

        options ??= new Text.FindReplaceOptions();
        var skip = new HashSet<string>(skipPaths ?? [], StringComparer.OrdinalIgnoreCase);
        var files = 0;
        foreach (var (path, text) in ReadMarkdown(folders))
        {
            if (skip.Contains(path))
            {
                continue;
            }

            var (updated, count) = Text.FindReplace.ReplaceAll(text, query, replacement, options);
            if (count == 0 || updated == text)
            {
                continue;
            }

            try
            {
                DocumentStore.WriteAtomic(path, updated);
                files++;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return files;
    }

    private static IReadOnlyList<WorkspaceEntry> FilterChildren(IReadOnlyList<WorkspaceEntry> entries, string query)
    {
        var result = new List<WorkspaceEntry>();
        foreach (var entry in entries)
        {
            if (entry.IsDirectory)
            {
                var children = FilterChildren(entry.Children, query);
                if (children.Count > 0 || entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry with { Children = children });
                }
            }
            else if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static WorkspaceEntry ScanDirectory(DirectoryInfo info, int depth, int maxDepth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var children = new List<WorkspaceEntry>();
        if (depth < maxDepth)
        {
            try
            {
                foreach (var directory in info.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (directory.Name.StartsWith('.') || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    children.Add(ScanDirectory(directory, depth + 1, maxDepth, cancellationToken));
                }

                foreach (var file in info.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!DocumentStore.IsMarkdown(file.FullName))
                    {
                        continue;
                    }

                    children.Add(new WorkspaceEntry(
                        file.FullName,
                        file.Name,
                        false,
                        file.LastWriteTimeUtc,
                        []));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return new WorkspaceEntry(info.FullName, info.Name, true, info.LastWriteTimeUtc, children);
    }
}

using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Markdown;

public sealed record WikiBacklink(string Path, string Name, int Line, string Preview);

public sealed record WikiGraphNode(string Path, string Stem, int Outgoing, int Incoming);

public sealed record WikiGraphEdge(string FromStem, string ToStem, bool Resolved);

public sealed record WikiGraph(IReadOnlyList<WikiGraphNode> Nodes, IReadOnlyList<WikiGraphEdge> Edges);

public static partial class WikiIndex
{
    public static string StemFromPath(string path)
        => Path.GetFileNameWithoutExtension(path ?? string.Empty);

    public static IReadOnlyList<WikiBacklink> FindBacklinks(
        IEnumerable<string> folders,
        string target,
        string? excludePath = null,
        int maxHits = 80)
    {
        target = (target ?? string.Empty).Trim();
        if (target.Length == 0)
        {
            return [];
        }

        maxHits = Math.Clamp(maxHits, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var notePath = Resolve(target, excludePath, folderList);
        var hits = new List<WikiBacklink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!seen.Add(path) ||
                (!string.IsNullOrEmpty(excludePath) &&
                 string.Equals(path, excludePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            foreach (var hit in WikiLinks.Find(text))
            {
                if (!IsBacklinkTo(hit.Target, target, notePath, path, folderList))
                {
                    continue;
                }

                var line = map.LineOfOffset(hit.Start);
                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, map.LineText(line).Trim()));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }

                break;
            }
        }

        return hits;
    }

    public static int RewriteAll(
        IEnumerable<string> folders,
        string oldTarget,
        string newTarget,
        IEnumerable<string>? skipPaths = null)
    {
        oldTarget = (oldTarget ?? string.Empty).Trim();
        newTarget = (newTarget ?? string.Empty).Trim();
        if (oldTarget.Length == 0 ||
            newTarget.Length == 0 ||
            string.Equals(oldTarget, newTarget, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var skip = new HashSet<string>(skipPaths ?? [], StringComparer.OrdinalIgnoreCase);
        var count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            if (!seen.Add(path) || skip.Contains(path))
            {
                continue;
            }

            if (!TryRead(path, out var text))
            {
                continue;
            }

            var rewritten = WikiLinks.RewriteTarget(text, oldTarget, newTarget);
            var oldFile = oldTarget.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? oldTarget : oldTarget + ".md";
            var newFile = newTarget.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? newTarget : newTarget + ".md";
            rewritten = WikiLinks.RewriteMarkdownFileLinks(rewritten, oldFile, newFile);
            if (rewritten == text)
            {
                continue;
            }

            try
            {
                DocumentStore.WriteAtomic(path, rewritten);
                count++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return count;
    }

    public static int RewriteHeadingAnchors(
        IEnumerable<string> folders,
        string stem,
        string oldHeading,
        string newHeading,
        IEnumerable<string>? skipPaths = null)
    {
        stem = (stem ?? string.Empty).Trim();
        oldHeading = (oldHeading ?? string.Empty).Trim();
        newHeading = (newHeading ?? string.Empty).Trim();
        if (oldHeading.Length == 0 ||
            newHeading.Length == 0 ||
            string.Equals(oldHeading, newHeading, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var skip = new HashSet<string>(skipPaths ?? [], StringComparer.OrdinalIgnoreCase);
        var count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            if (!seen.Add(path) || skip.Contains(path) || !TryRead(path, out var text))
            {
                continue;
            }

            var rewritten = WikiLinks.RewriteHeading(text, stem, oldHeading, newHeading);
            if (string.Equals(StemFromPath(path), stem, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = WikiLinks.RewriteHeading(rewritten, string.Empty, oldHeading, newHeading);
            }

            if (rewritten == text)
            {
                continue;
            }

            try
            {
                DocumentStore.WriteAtomic(path, rewritten);
                count++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return count;
    }

    public static string? Resolve(string target, string? documentPath, IEnumerable<string>? folders = null)
    {
        target = (target ?? string.Empty).Trim().Replace('\\', '/');
        if (target.Length == 0)
        {
            if (!string.IsNullOrWhiteSpace(documentPath) && File.Exists(documentPath))
            {
                return Path.GetFullPath(documentPath);
            }

            return null;
        }

        if (IsMediaTarget(target))
        {
            return ResolveMedia(target, documentPath, folders);
        }

        if (target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            target = target[..^3];
        }

        if (target.Length == 0)
        {
            return null;
        }

        if (Path.IsPathRooted(target))
        {
            var rooted = target.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? target : target + ".md";
            return File.Exists(rooted) ? Path.GetFullPath(rooted) : null;
        }

        if (!string.IsNullOrWhiteSpace(documentPath))
        {
            try
            {
                var root = Path.GetDirectoryName(documentPath);
                if (!string.IsNullOrEmpty(root))
                {
                    var sibling = Path.GetFullPath(Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar) + ".md"));
                    if (File.Exists(sibling))
                    {
                        return sibling;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        var stem = Path.GetFileName(target);
        string? aliasHit = null;
        foreach (var path in MarkdownPaths(folders ?? []))
        {
            if (string.Equals(StemFromPath(path), stem, StringComparison.OrdinalIgnoreCase))
            {
                if (target.Contains('/', StringComparison.Ordinal))
                {
                    var normalized = path.Replace('\\', '/');
                    if (normalized.EndsWith("/" + target + ".md", StringComparison.OrdinalIgnoreCase) ||
                        normalized.EndsWith(target + ".md", StringComparison.OrdinalIgnoreCase))
                    {
                        return path;
                    }

                    continue;
                }

                return path;
            }

            if (aliasHit is null &&
                TryRead(path, out var text) &&
                FrontMatter.TrySplit(text, out var fields, out _) &&
                (FrontMatter.Aliases(fields).Any(alias =>
                     string.Equals(alias, stem, StringComparison.OrdinalIgnoreCase)) ||
                 string.Equals(FrontMatter.Id(fields), stem, StringComparison.OrdinalIgnoreCase)))
            {
                aliasHit = path;
            }
        }

        return aliasHit;
    }

    public static bool IsImageTarget(string target)
    {
        var ext = Path.GetExtension(target ?? string.Empty);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMediaTarget(string target)
    {
        if (IsImageTarget(target))
        {
            return true;
        }

        var ext = Path.GetExtension(target ?? string.Empty);
        return ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".wav", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ResolveMedia(string target, string? documentPath, IEnumerable<string>? folders = null)
    {
        target = (target ?? string.Empty).Trim().Replace('\\', '/');
        if (target.Length == 0)
        {
            return null;
        }

        if (Path.IsPathRooted(target))
        {
            return File.Exists(target) ? Path.GetFullPath(target) : null;
        }

        var name = Path.GetFileName(target);
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(documentPath))
        {
            try
            {
                var root = Path.GetDirectoryName(documentPath);
                if (!string.IsNullOrEmpty(root))
                {
                    candidates.Add(Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar)));
                    candidates.Add(Path.Combine(root, VaultAttachments.FolderName, name));
                }
            }
            catch (Exception)
            {
            }
        }

        foreach (var folder in folders ?? [])
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            candidates.Add(Path.Combine(folder, target.Replace('/', Path.DirectorySeparatorChar)));
            candidates.Add(Path.Combine(folder, VaultAttachments.FolderName, name));
        }

        foreach (var candidate in candidates)
        {
            try
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch (Exception)
            {
            }
        }

        return null;
    }

    public static IReadOnlyList<WikiBacklink> MissingMedia(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        var folderList = folders?.ToList() ?? [];
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            foreach (var hit in WikiLinks.Find(text))
            {
                if (!IsMediaTarget(hit.Target) || ResolveMedia(hit.Target, path, folderList) is not null)
                {
                    continue;
                }

                var line = map.LineOfOffset(hit.Start);
                hits.Add(new WikiBacklink(
                    path,
                    Path.GetFileName(path),
                    line + 1,
                    (hit.IsEmbed ? "![[" : "[[") + hit.Target + "]]"));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }

                break;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> BrokenEmbeds(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            foreach (var hit in WikiLinks.Find(text))
            {
                if (!hit.IsEmbed)
                {
                    continue;
                }

                var resolved = IsMediaTarget(hit.Target)
                    ? ResolveMedia(hit.Target, path, folderList)
                    : Resolve(hit.Target, path, folderList);
                if (resolved is not null)
                {
                    continue;
                }

                hits.Add(new WikiBacklink(
                    path,
                    Path.GetFileName(path),
                    map.LineOfOffset(hit.Start) + 1,
                    "![[" + hit.Target + "]]"));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }

                break;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> UnusedAttachments(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            CollectReferencedMedia(text, path, folderList, referenced);
        }

        var hits = new List<WikiBacklink>();
        foreach (var folder in folderList)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var assets = Path.Combine(folder, VaultAttachments.FolderName);
            if (!Directory.Exists(assets))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(assets);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                string full;
                try
                {
                    full = Path.GetFullPath(file);
                }
                catch (Exception)
                {
                    continue;
                }

                if (referenced.Contains(full))
                {
                    continue;
                }

                hits.Add(new WikiBacklink(full, Path.GetFileName(file), 1, Path.GetFileName(file)));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    private static void CollectReferencedMedia(
        string markdown,
        string path,
        IEnumerable<string> folders,
        HashSet<string> referenced)
    {
        foreach (var hit in WikiLinks.Find(markdown))
        {
            if (!IsMediaTarget(hit.Target))
            {
                continue;
            }

            var resolved = Resolve(hit.Target, path, folders);
            if (resolved is not null)
            {
                referenced.Add(Path.GetFullPath(resolved));
            }
        }

        try
        {
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                         markdown,
                         @"\]\(([^)]+)\)",
                         System.Text.RegularExpressions.RegexOptions.None,
                         TimeSpan.FromMilliseconds(80)))
            {
                var url = match.Groups[1].Value.Trim().Trim('"');
                if (url.Length == 0 ||
                    url.StartsWith('#') ||
                    url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var hash = url.IndexOf('#');
                if (hash >= 0)
                {
                    url = url[..hash];
                }

                try
                {
                    var root = Path.GetDirectoryName(path);
                    if (string.IsNullOrEmpty(root))
                    {
                        continue;
                    }

                    var candidate = Path.IsPathRooted(url)
                        ? url
                        : Path.GetFullPath(Path.Combine(root, url.Replace('/', Path.DirectorySeparatorChar)));
                    if (File.Exists(candidate))
                    {
                        referenced.Add(candidate);
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
        }
    }

    public static string? Relocate(
        string path,
        string destinationFolder,
        IEnumerable<string>? folders = null,
        IEnumerable<string>? skipPaths = null)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path) ||
            string.IsNullOrWhiteSpace(destinationFolder) ||
            !Directory.Exists(destinationFolder))
        {
            return null;
        }

        var folderList = folders?.ToList() ?? [];
        string dest;
        try
        {
            if (string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(destinationFolder), StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(path);
            }

            dest = VaultAttachments.UniquePath(destinationFolder, StemFromPath(path), Path.GetExtension(path));
            if (string.Equals(path, dest, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(path);
            }

            File.Move(path, dest);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var skip = new HashSet<string>(skipPaths ?? [], StringComparer.OrdinalIgnoreCase);
        if (skip.Contains(path)) skip.Add(dest);
        foreach (var referringPath in MarkdownPaths(folderList).Append(dest).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (skip.Contains(referringPath) || !TryRead(referringPath, out var text)) continue;
            var originalDocumentPath = string.Equals(referringPath, dest, StringComparison.OrdinalIgnoreCase) ? path : referringPath;
            var rewritten = RewriteRelocatedLinks(text, originalDocumentPath, path, dest, folderList);
            if (rewritten == text) continue;
            try
            {
                DocumentStore.WriteAtomic(referringPath, rewritten);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return dest;
    }

    /// <summary>Uses the same relocation rules for unsaved buffers and files on disk.</summary>
    public static string RewriteRelocatedLinks(
        string text,
        string? documentPath,
        string oldPath,
        string newPath,
        IEnumerable<string>? folders = null)
    {
        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return text;
        var folderList = folders?.ToList() ?? [];
        var oldFile = VaultAttachments.RelativeToVault(oldPath, folderList)!;
        var newFile = VaultAttachments.RelativeToVault(newPath, folderList)!;
        // A destination outside the indexed folders needs an absolute target to stay resolvable.
        if (folderList.Count > 0 && !folderList.Any(folder => IsUnderFolder(newPath, folder)))
            newFile = Path.GetFullPath(newPath).Replace('\\', '/');
        var oldTarget = Path.ChangeExtension(oldFile, null);
        var newTarget = Path.ChangeExtension(newFile, null);
        var rewritten = WikiLinks.RewriteTarget(text, oldTarget, newTarget);
        rewritten = WikiLinks.RewriteTarget(rewritten, oldFile, newFile);

        if (string.Equals(documentPath, oldPath, StringComparison.OrdinalIgnoreCase))
            return MarkdownLinkRelocation.Rebase(rewritten, oldPath, newPath);

        if (documentPath is { Length: > 0 } && Path.GetDirectoryName(documentPath) is { Length: > 0 } directory)
        {
            oldFile = Path.GetRelativePath(directory, oldPath).Replace('\\', '/');
            newFile = Path.GetRelativePath(directory, newPath).Replace('\\', '/');
        }
        return WikiLinks.RewriteMarkdownFileLinks(rewritten, oldFile, newFile);
    }

    private static bool IsUnderFolder(string path, string folder)
    {
        var root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<WikiBacklink> BrokenMarkdownLinks(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        var folderList = folders?.ToList() ?? [];
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            try
            {
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                             text,
                             @"\[[^\]]*\]\(([^)]+)\)",
                             System.Text.RegularExpressions.RegexOptions.None,
                             TimeSpan.FromMilliseconds(80)))
                {
                    var url = match.Groups[1].Value.Trim().Trim('"');
                    if (url.Length == 0 ||
                        url.StartsWith('#') ||
                        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var hash = url.IndexOf('#');
                    var file = hash > 0 ? url[..hash] : url;
                    if (LocalFileExists(file, path))
                    {
                        continue;
                    }

                    if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                        Resolve(Path.GetFileNameWithoutExtension(file), path, folderList) is not null)
                    {
                        continue;
                    }

                    var line = map.LineOfOffset(match.Index);
                    hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, url));
                    if (hits.Count >= maxHits)
                    {
                        return hits;
                    }

                    break;
                }
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> BrokenImages(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        var folderList = folders?.ToList() ?? [];
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            try
            {
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                             text,
                             @"!\[[^\]]*\]\(([^)]+)\)",
                             System.Text.RegularExpressions.RegexOptions.None,
                             TimeSpan.FromMilliseconds(80)))
                {
                    var url = match.Groups[1].Value.Trim().Trim('"');
                    if (url.Length == 0 ||
                        url.StartsWith('#') ||
                        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var hash = url.IndexOf('#');
                    var file = hash > 0 ? url[..hash] : url;
                    if (LocalFileExists(file, path) || ResolveMedia(file, path, folderList) is not null)
                    {
                        continue;
                    }

                    var line = map.LineOfOffset(match.Index);
                    hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, url));
                    if (hits.Count >= maxHits)
                    {
                        return hits;
                    }

                    break;
                }
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> BrokenHeadings(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            foreach (var hit in WikiLinks.Find(text))
            {
                if (string.IsNullOrEmpty(hit.Heading) ||
                    hit.Heading.StartsWith('^') ||
                    IsMediaTarget(hit.Target))
                {
                    continue;
                }

                var resolved = Resolve(hit.Target, path, folderList);
                if (resolved is null)
                {
                    continue;
                }

                var source = string.Equals(resolved, path, StringComparison.OrdinalIgnoreCase)
                    ? text
                    : TryRead(resolved, out var other)
                        ? other
                        : null;
                if (source is null || ContainsHeading(source, hit.Heading))
                {
                    continue;
                }

                var preview = (hit.Target.Length == 0 ? string.Empty : hit.Target) + "#" + hit.Heading;
                hits.Add(new WikiBacklink(path, Path.GetFileName(path), map.LineOfOffset(hit.Start) + 1, preview));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }

                break;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> BrokenBlockRefs(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            foreach (var hit in WikiLinks.Find(text))
            {
                if (string.IsNullOrEmpty(hit.Heading) ||
                    !hit.Heading.StartsWith('^') ||
                    IsMediaTarget(hit.Target))
                {
                    continue;
                }

                var resolved = Resolve(hit.Target, path, folderList);
                if (resolved is null)
                {
                    continue;
                }

                var source = string.Equals(resolved, path, StringComparison.OrdinalIgnoreCase)
                    ? text
                    : TryRead(resolved, out var other)
                        ? other
                        : null;
                if (source is null || ContainsBlockId(source, hit.Heading))
                {
                    continue;
                }

                var preview = (hit.Target.Length == 0 ? string.Empty : hit.Target) + "#" + hit.Heading;
                hits.Add(new WikiBacklink(path, Path.GetFileName(path), map.LineOfOffset(hit.Start) + 1, preview));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }

                break;
            }
        }

        return hits;
    }

    public static string ResolveOrFallback(string target, string? documentPath, IEnumerable<string>? folders = null)
    {
        var resolved = Resolve(target, documentPath, folders);
        if (resolved is not null)
        {
            return resolved;
        }

        var file = target.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? target : target + ".md";
        if (!string.IsNullOrWhiteSpace(documentPath))
        {
            try
            {
                var root = Path.GetDirectoryName(documentPath);
                if (!string.IsNullOrEmpty(root))
                {
                    return Path.GetFullPath(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
            catch (Exception)
            {
            }
        }

        return file;
    }

    public static string ExtractHeadingSection(string markdown, string? heading)
    {
        markdown ??= string.Empty;
        heading = (heading ?? string.Empty).Trim();
        if (heading.Length == 0)
        {
            return markdown;
        }

        var map = new LineMap(markdown);
        if (heading.StartsWith('^'))
        {
            return ExtractBlock(map, heading.TrimStart('^').Trim()) ?? markdown;
        }

        var section = TocExtractor.Extract(markdown).FirstOrDefault(node => MatchesHeading(node, heading));
        return section is null
            ? markdown
            : map.Text[map.OffsetOfLine(section.SourceLine)..map.OffsetOfLine(section.SourceEndLine + 1)];
    }

    public static bool ContainsHeading(string markdown, string heading)
    {
        markdown ??= string.Empty;
        heading = (heading ?? string.Empty).Trim();
        if (heading.Length == 0)
        {
            return false;
        }

        if (heading.StartsWith('^'))
        {
            return ExtractBlock(new LineMap(markdown), heading.TrimStart('^').Trim()) is not null;
        }

        return TocExtractor.Extract(markdown).Any(node => MatchesHeading(node, heading));
    }

    public static string NoteTitle(string path, string? markdown = null)
    {
        if (markdown is null && !TryRead(path, out markdown))
        {
            return StemFromPath(path);
        }

        if (FrontMatter.TrySplit(markdown, out var fields, out _) &&
            fields.TryGetValue("title", out var title) &&
            !string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        return TocExtractor.Extract(markdown).FirstOrDefault()?.Title ?? StemFromPath(path);
    }

    private static string? ExtractBlock(LineMap map, string id)
    {
        if (id.Length == 0)
        {
            return null;
        }

        var marker = "^" + id;
        var codeLines = MarkdownFence.CodeLines(map);
        for (var i = 0; i < map.LineCount; i++)
        {
            if (codeLines[i])
            {
                continue;
            }

            var trimmed = map.LineText(i).TrimEnd();
            if (!trimmed.Equals(marker, StringComparison.OrdinalIgnoreCase) &&
                !trimmed.EndsWith(" " + marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var startLine = i;
            while (startLine > 0)
            {
                var previous = map.LineText(startLine - 1);
                if (codeLines[startLine - 1] || previous.Trim().Length == 0 || TryAtxHeading(previous, out _, out _))
                {
                    break;
                }

                startLine--;
            }

            return map.Text[map.OffsetOfLine(startLine)..map.OffsetOfLine(i + 1)];
        }

        return null;
    }

    private static bool TryAtxHeading(string line, out int level, out string title)
    {
        level = 0;
        title = string.Empty;
        var trimmed = line.TrimStart();
        var i = 0;
        while (i < trimmed.Length && trimmed[i] == '#')
        {
            i++;
        }

        if (i is < 1 or > 6 || i >= trimmed.Length || trimmed[i] != ' ')
        {
            return false;
        }

        level = i;
        title = trimmed[i..].Trim();
        return title.Length > 0;
    }

    private static bool MatchesHeading(OutlineNode node, string heading)
        => string.Equals(node.Title, heading, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(node.Id, heading, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(MarkdownEditing.HeadingAnchor(node.Title), MarkdownEditing.HeadingAnchor(heading), StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Catalog(IEnumerable<string> folders)
    {
        var stems = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (stem.Length > 0)
            {
                stems.Add(stem);
            }

            if (TryRead(path, out var text) && FrontMatter.TrySplit(text, out var fields, out _))
            {
                foreach (var alias in FrontMatter.Aliases(fields))
                {
                    stems.Add(alias);
                }

                var id = FrontMatter.Id(fields);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    stems.Add(id);
                }
            }
        }

        return stems.ToList();
    }

    public static IReadOnlyList<string> AllTags(IEnumerable<string> folders, int max = 80)
    {
        max = Math.Clamp(max, 1, 200);
        var tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            FrontMatter.TrySplit(text, out var fields, out _);
            foreach (var tag in MarkdownTags.Extract(text, fields))
            {
                tags.Add(tag);
                if (tags.Count >= max)
                {
                    return tags.ToList();
                }
            }
        }

        return tags.ToList();
    }

    public static IReadOnlyList<WikiBacklink> Unresolved(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        var folderList = folders?.ToList() ?? [];
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            foreach (var hit in WikiLinks.Find(text))
            {
                if (Resolve(hit.Target, path, folderList) is not null)
                {
                    continue;
                }

                var line = map.LineOfOffset(hit.Start);
                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, "[[" + hit.Target + "]]"));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }

                break;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> UnlinkedMentions(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var files = MarkdownPaths(folderList)
            .Select(path => (Path: path, Stem: StemFromPath(path)))
            .Where(item => item.Stem.Length >= 3)
            .ToList();
        var hits = new List<WikiBacklink>();
        foreach (var source in files)
        {
            if (!TryRead(source.Path, out var text))
            {
                continue;
            }

            var wiki = WikiLinks.Find(text).ToList();
            var map = new LineMap(text);
            foreach (var target in files)
            {
                if (string.Equals(source.Path, target.Path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (wiki.Any(hit => string.Equals(hit.Target, target.Stem, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var index = IndexOfMention(text, target.Stem, wiki);
                if (index < 0)
                {
                    continue;
                }

                hits.Add(new WikiBacklink(
                    source.Path,
                    Path.GetFileName(source.Path),
                    map.LineOfOffset(index) + 1,
                    target.Stem));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }

                break;
            }
        }

        return hits;
    }

    private static int IndexOfMention(string text, string stem, IReadOnlyList<WikiLinkHit> wiki)
    {
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(stem, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return -1;
            }

            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var after = index + stem.Length >= text.Length || !char.IsLetterOrDigit(text[index + stem.Length]);
            var insideWiki = wiki.Any(hit => index >= hit.Start && index < hit.Start + hit.Length);
            if (before && after && !insideWiki)
            {
                return index;
            }

            start = index + stem.Length;
        }

        return -1;
    }

    public static string Linkify(string markdown, string stem)
    {
        markdown ??= string.Empty;
        stem = (stem ?? string.Empty).Trim();
        if (stem.Length < 2 || markdown.Length == 0)
        {
            return markdown;
        }

        var wiki = WikiLinks.Find(markdown).ToList();
        var builder = new System.Text.StringBuilder(markdown.Length + 8);
        var cursor = 0;
        var map = new LineMap(markdown);
        var codeLines = MarkdownFence.CodeLines(map);
        for (var line = 0; line < map.LineCount; line++)
        {
            var lineText = map.LineText(line);
            var offset = map.OffsetOfLine(line);
            if (codeLines[line])
            {
                continue;
            }

            var search = 0;
            while (search < lineText.Length)
            {
                var local = lineText.IndexOf(stem, search, StringComparison.OrdinalIgnoreCase);
                if (local < 0)
                {
                    break;
                }

                var absolute = offset + local;
                var before = local == 0 || !char.IsLetterOrDigit(lineText[local - 1]);
                var after = local + stem.Length >= lineText.Length || !char.IsLetterOrDigit(lineText[local + stem.Length]);
                var insideWiki = wiki.Any(hit => absolute >= hit.Start && absolute < hit.Start + hit.Length);
                if (before && after && !insideWiki)
                {
                    builder.Append(markdown, cursor, absolute - cursor);
                    builder.Append("[[").Append(stem).Append("]]");
                    cursor = absolute + stem.Length;
                    search = local + stem.Length;
                    continue;
                }

                search = local + stem.Length;
            }
        }

        builder.Append(markdown, cursor, markdown.Length - cursor);
        return builder.ToString();
    }

    public static IReadOnlyList<WikiBacklink> Outgoing(
        string markdown,
        string? documentPath,
        IEnumerable<string>? folders = null,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        markdown ??= string.Empty;
        if (markdown.Length == 0)
        {
            return [];
        }

        var folderList = folders?.ToList() ?? [];
        var map = new LineMap(markdown);
        var hits = new List<WikiBacklink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hit in WikiLinks.Find(markdown))
        {
            var key = hit.Target + "#" + (hit.Heading ?? string.Empty);
            if (!seen.Add(key))
            {
                continue;
            }

            var path = ResolveOrFallback(hit.Target, documentPath, folderList);
            var line = map.LineOfOffset(hit.Start);
            var preview = hit.Heading is { Length: > 0 } ? hit.Target + "#" + hit.Heading : hit.Target;
            hits.Add(new WikiBacklink(path, hit.Target, line + 1, "[[" + preview + "]]"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> FilesWithTag(IEnumerable<string> folders, string tag, int maxHits = 80)
    {
        tag = (tag ?? string.Empty).Trim().TrimStart('#');
        if (tag.Length == 0)
        {
            return [];
        }

        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            FrontMatter.TrySplit(text, out var fields, out _);
            if (!MarkdownTags.Extract(text, fields).Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "#" + tag));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> FilesWithAllTags(
        IEnumerable<string> folders,
        IEnumerable<string> tags,
        int maxHits = 80)
    {
        var required = (tags ?? [])
            .Select(tag => (tag ?? string.Empty).Trim().TrimStart('#'))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (required.Count == 0)
        {
            return [];
        }

        if (required.Count == 1)
        {
            return FilesWithTag(folders, required[0], maxHits);
        }

        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            FrontMatter.TrySplit(text, out var fields, out _);
            var found = MarkdownTags.Extract(text, fields);
            if (!required.TrueForAll(tag => found.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, string.Join(" ", required.Select(tag => "#" + tag))));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> FilesWithAnyTag(
        IEnumerable<string> folders,
        IEnumerable<string> tags,
        int maxHits = 80)
    {
        var wanted = (tags ?? [])
            .Select(tag => (tag ?? string.Empty).Trim().TrimStart('#'))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        if (wanted.Count == 1)
        {
            return FilesWithTag(folders, wanted[0], maxHits);
        }

        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            FrontMatter.TrySplit(text, out var fields, out _);
            var found = MarkdownTags.Extract(text, fields);
            var matched = wanted.Where(tag => found.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
            if (matched.Count == 0)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, string.Join(" ", matched.Select(tag => "#" + tag))));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> Orphans(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var paths = MarkdownPaths(folderList).ToList();
        var targeted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            foreach (var hit in WikiLinks.Find(text))
            {
                var resolved = Resolve(hit.Target, path, folderList);
                if (resolved is not null &&
                    !string.Equals(path, resolved, StringComparison.OrdinalIgnoreCase))
                {
                    targeted.Add(resolved);
                }
            }
        }

        var orphans = new List<WikiBacklink>();
        foreach (var path in paths)
        {
            if (targeted.Contains(path))
            {
                continue;
            }

            orphans.Add(new WikiBacklink(path, Path.GetFileName(path), 1, Path.GetFileNameWithoutExtension(path)));
            if (orphans.Count >= maxHits)
            {
                return orphans;
            }
        }

        return orphans;
    }

    public static WikiGraph Graph(IEnumerable<string> folders, int maxNodes = 80)
    {
        maxNodes = Math.Clamp(maxNodes, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var paths = MarkdownPaths(folderList).ToList();
        var outgoing = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var incoming = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var pathByStem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edgeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<WikiGraphEdge>();

        foreach (var path in paths)
        {
            var stem = StemFromPath(path);
            if (stem.Length == 0)
            {
                continue;
            }

            pathByStem[stem] = path;
            outgoing[stem] = [];
            if (!incoming.ContainsKey(stem))
            {
                incoming[stem] = [];
            }
        }

        foreach (var path in paths)
        {
            var from = StemFromPath(path);
            if (from.Length == 0 || !TryRead(path, out var text))
            {
                continue;
            }

            foreach (var hit in WikiLinks.Find(text))
            {
                var to = hit.Target.Trim();
                if (to.Length == 0)
                {
                    continue;
                }

                var resolved = Resolve(hit.Target, path, folderList);
                if (resolved is not null && DocumentStore.IsMarkdown(resolved))
                {
                    to = StemFromPath(resolved);
                }

                outgoing[from].Add(to);
                if (!incoming.ContainsKey(to))
                {
                    incoming[to] = [];
                }

                incoming[to].Add(from);
                var key = from + "\n" + to;
                if (!edgeKeys.Add(key))
                {
                    continue;
                }

                edges.Add(new WikiGraphEdge(from, to, resolved is not null));
            }
        }

        var nodes = pathByStem
            .Select(pair => new WikiGraphNode(
                pair.Value,
                pair.Key,
                outgoing.TryGetValue(pair.Key, out var outs) ? outs.Count : 0,
                incoming.TryGetValue(pair.Key, out var ins) ? ins.Count : 0))
            .OrderByDescending(node => node.Incoming + node.Outgoing)
            .ThenBy(node => node.Stem, StringComparer.OrdinalIgnoreCase)
            .Take(maxNodes)
            .ToList();

        var keep = new HashSet<string>(nodes.Select(node => node.Stem), StringComparer.OrdinalIgnoreCase);
        return new WikiGraph(
            nodes,
            edges.Where(edge => keep.Contains(edge.FromStem) || keep.Contains(edge.ToStem)).ToList());
    }

    public static WikiGraph Ego(IEnumerable<string> folders, string stem, int maxNodes = 40)
    {
        stem = (stem ?? string.Empty).Trim();
        maxNodes = Math.Clamp(maxNodes, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var full = Graph(folderList, 200);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (stem.Length > 0)
        {
            keep.Add(stem);
        }

        foreach (var edge in full.Edges)
        {
            if (string.Equals(edge.FromStem, stem, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(edge.ToStem, stem, StringComparison.OrdinalIgnoreCase))
            {
                keep.Add(edge.FromStem);
                keep.Add(edge.ToStem);
            }
        }

        var nodes = full.Nodes
            .Where(node => keep.Contains(node.Stem))
            .OrderByDescending(node => string.Equals(node.Stem, stem, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(node => node.Incoming + node.Outgoing)
            .Take(maxNodes)
            .ToList();

        if (stem.Length > 0 &&
            nodes.TrueForAll(node => !string.Equals(node.Stem, stem, StringComparison.OrdinalIgnoreCase)))
        {
            var path = Resolve(stem, null, folderList);
            if (path is not null)
            {
                nodes.Insert(0, new WikiGraphNode(path, stem, 0, 0));
                if (nodes.Count > maxNodes)
                {
                    nodes.RemoveAt(nodes.Count - 1);
                }
            }
        }

        keep = new HashSet<string>(nodes.Select(node => node.Stem), StringComparer.OrdinalIgnoreCase);
        var edges = full.Edges
            .Where(edge => keep.Contains(edge.FromStem) && keep.Contains(edge.ToStem))
            .ToList();
        return new WikiGraph(nodes, edges);
    }

    public static IReadOnlyList<WikiBacklink> DeadEnds(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        return Graph(folders, 200).Nodes
            .Where(node => node.Outgoing == 0)
            .Take(maxHits)
            .Select(node => new WikiBacklink(node.Path, Path.GetFileName(node.Path), 1, node.Stem))
            .ToList();
    }

    public static IReadOnlyList<WikiBacklink> Hubs(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        return Graph(folders, 200).Nodes
            .Where(node => node.Incoming >= 2)
            .OrderByDescending(node => node.Incoming)
            .ThenBy(node => node.Stem, StringComparer.OrdinalIgnoreCase)
            .Take(maxHits)
            .Select(node => new WikiBacklink(node.Path, Path.GetFileName(node.Path), 1, node.Incoming.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
    }

    public static IReadOnlyList<WikiBacklink> Sources(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        return Graph(folders, 200).Nodes
            .Where(node => node.Incoming == 0 && node.Outgoing > 0)
            .OrderByDescending(node => node.Outgoing)
            .ThenBy(node => node.Stem, StringComparer.OrdinalIgnoreCase)
            .Take(maxHits)
            .Select(node => new WikiBacklink(node.Path, Path.GetFileName(node.Path), 1, node.Stem))
            .ToList();
    }

    public static IReadOnlyList<WikiBacklink> MutualLinks(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var graph = Graph(folders, 200);
        var byStem = graph.Nodes.ToDictionary(node => node.Stem, StringComparer.OrdinalIgnoreCase);
        var edges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            edges.Add(edge.FromStem + "\n" + edge.ToStem);
        }

        var hits = new List<WikiBacklink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            if (!edges.Contains(edge.ToStem + "\n" + edge.FromStem))
            {
                continue;
            }

            var key = string.Compare(edge.FromStem, edge.ToStem, StringComparison.OrdinalIgnoreCase) <= 0
                ? edge.FromStem + "\n" + edge.ToStem
                : edge.ToStem + "\n" + edge.FromStem;
            if (!seen.Add(key) || !byStem.TryGetValue(edge.FromStem, out var node))
            {
                continue;
            }

            hits.Add(new WikiBacklink(node.Path, Path.GetFileName(node.Path), 1, "↔ " + edge.ToStem));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static bool IsDailyStem(string stem)
        => DateTime.TryParseExact(
            stem ?? string.Empty,
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out _);

    public static string? ExistingDaily(IEnumerable<string> folders, DateTime date)
    {
        var stem = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var path in MarkdownPaths(folders))
        {
            if (string.Equals(StemFromPath(path), stem, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    public static string WeeklyStem(DateTime date)
        => MarkdownTemplates.WeeklyStem(date);

    public static bool IsWeeklyStem(string stem)
    {
        stem = (stem ?? string.Empty).Trim();
        if (stem.Length < 8 || stem[4] != '-')
        {
            return false;
        }

        if (stem[5] is not 'W' and not 'w')
        {
            return false;
        }

        return int.TryParse(stem[..4], out var year) &&
               year is >= 1990 and <= 2100 &&
               int.TryParse(stem[6..], out var week) &&
               week is >= 1 and <= 53;
    }

    public static string? ExistingWeekly(IEnumerable<string> folders, DateTime date)
    {
        var stem = WeeklyStem(date);
        foreach (var path in MarkdownPaths(folders))
        {
            if (string.Equals(StemFromPath(path), stem, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    public static IReadOnlyList<WikiBacklink> WeeklyFiles(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (!IsWeeklyStem(stem))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, stem));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static string MonthlyStem(DateTime date)
        => date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsMonthlyStem(string stem)
        => DateTime.TryParseExact(
            stem ?? string.Empty,
            "yyyy-MM",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out _);

    public static string? ExistingMonthly(IEnumerable<string> folders, DateTime date)
    {
        var stem = MonthlyStem(date);
        foreach (var path in MarkdownPaths(folders))
        {
            if (string.Equals(StemFromPath(path), stem, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    public static IReadOnlyList<WikiBacklink> MonthlyFiles(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (!IsMonthlyStem(stem))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, stem));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static string YearlyStem(DateTime date)
        => date.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsYearlyStem(string stem)
    {
        stem = (stem ?? string.Empty).Trim();
        return stem.Length == 4 &&
               int.TryParse(stem, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var year) &&
               year is >= 1990 and <= 2100;
    }

    public static string? ExistingYearly(IEnumerable<string> folders, DateTime date)
    {
        var stem = YearlyStem(date);
        foreach (var path in MarkdownPaths(folders))
        {
            if (string.Equals(StemFromPath(path), stem, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    public static IReadOnlyList<WikiBacklink> YearlyFiles(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (!IsYearlyStem(stem))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, stem));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static string QuarterlyStem(DateTime date)
        => MarkdownTemplates.QuarterlyStem(date);

    public static bool IsQuarterlyStem(string stem)
    {
        stem = (stem ?? string.Empty).Trim();
        return stem.Length == 7 &&
               stem[4] == '-' &&
               stem[5] is 'Q' or 'q' &&
               stem[6] is >= '1' and <= '4' &&
               int.TryParse(stem[..4], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var year) &&
               year is >= 1990 and <= 2100;
    }

    public static string? ExistingQuarterly(IEnumerable<string> folders, DateTime date)
    {
        var stem = QuarterlyStem(date);
        foreach (var path in MarkdownPaths(folders))
        {
            if (string.Equals(StemFromPath(path), stem, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    public static IReadOnlyList<WikiBacklink> QuarterlyFiles(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (!IsQuarterlyStem(stem))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, stem));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> DailyNotes(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            var daily = IsDailyStem(stem);
            if (!daily && TryRead(path, out var text))
            {
                FrontMatter.TrySplit(text, out var fields, out _);
                daily = MarkdownTags.Extract(text, fields).Contains("daily", StringComparer.OrdinalIgnoreCase);
            }

            if (!daily)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, stem));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> OpenTasks(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            for (var line = 0; line < map.LineCount; line++)
            {
                var trimmed = map.LineText(line).TrimStart();
                if (!trimmed.StartsWith("- [ ] ", StringComparison.Ordinal) &&
                    !trimmed.StartsWith("* [ ] ", StringComparison.Ordinal))
                {
                    continue;
                }

                var title = trimmed[6..].Trim();
                if (title.Length == 0)
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, title));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> CompletedTasks(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            for (var line = 0; line < map.LineCount; line++)
            {
                var trimmed = map.LineText(line).TrimStart();
                if (!trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var title = trimmed[6..].Trim();
                if (title.Length == 0)
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, title));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> DueTasks(
        IEnumerable<string> folders,
        DateTime? on = null,
        bool overdueOnly = false,
        DateTime? today = null,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var day = (today ?? DateTime.Today).Date;
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            for (var line = 0; line < map.LineCount; line++)
            {
                var trimmed = map.LineText(line).TrimStart();
                if (!trimmed.StartsWith("- [ ] ", StringComparison.Ordinal) &&
                    !trimmed.StartsWith("* [ ] ", StringComparison.Ordinal))
                {
                    continue;
                }

                var title = trimmed[6..].Trim();
                var due = TaskDueDate(title);
                if (due is null)
                {
                    continue;
                }

                if (overdueOnly && due.Value.Date >= day)
                {
                    continue;
                }

                if (on is not null && due.Value.Date != on.Value.Date)
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, title));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static DateTime? TaskDueDate(string line)
    {
        line ??= string.Empty;
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line,
                @"\b(\d{4}-\d{2}-\d{2})\b",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(80));
            if (match.Success &&
                DateTime.TryParseExact(
                    match.Groups[1].Value,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var due))
            {
                return due.Date;
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
        }

        return null;
    }

    public static string? PickRandom(IEnumerable<string> folders, Random? random = null)
    {
        var paths = MarkdownPaths(folders).ToList();
        if (paths.Count == 0)
        {
            return null;
        }

        random ??= Random.Shared;
        return paths[random.Next(paths.Count)];
    }

    public static IReadOnlyList<WikiBacklink> WeekNotes(
        IEnumerable<string> folders,
        DateTime? around = null,
        int maxHits = 14)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var day = (around ?? DateTime.Today).Date;
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var hits = new List<WikiBacklink>();
        for (var i = 0; i < 7 && hits.Count < maxHits; i++)
        {
            var date = monday.AddDays(i);
            var stem = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var path = ExistingDaily(folders, date);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, stem));
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> RecentNotes(IEnumerable<string> folders, int maxHits = 40)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var ranked = new List<(string Path, DateTime Utc)>();
        foreach (var path in MarkdownPaths(folders))
        {
            try
            {
                ranked.Add((path, File.GetLastWriteTimeUtc(path)));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return ranked
            .OrderByDescending(item => item.Utc)
            .Take(maxHits)
            .Select(item => new WikiBacklink(
                item.Path,
                Path.GetFileName(item.Path),
                1,
                item.Utc.ToString("u")))
            .ToList();
    }

    public static IReadOnlyList<WikiBacklink> NotesWithAliases(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !FrontMatter.TrySplit(text, out var fields, out _))
            {
                continue;
            }

            var aliases = FrontMatter.Aliases(fields);
            if (aliases.Count == 0)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, string.Join(", ", aliases)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static string ToMermaid(WikiGraph graph)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("flowchart LR");
        if (graph is null || graph.Nodes.Count == 0)
        {
            builder.AppendLine("    empty[Vault]");
            return builder.ToString();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in graph.Edges)
        {
            var from = MermaidId(edge.FromStem);
            var to = MermaidId(edge.ToStem);
            var arrow = edge.Resolved ? "-->" : "-.->";
            builder.Append("    ")
                .Append(from).Append('[').Append(SanitizeLabel(edge.FromStem)).Append("] ")
                .Append(arrow)
                .Append(' ')
                .Append(to).Append('[').Append(SanitizeLabel(edge.ToStem)).AppendLine("]");
            seen.Add(edge.FromStem);
            seen.Add(edge.ToStem);
        }

        foreach (var node in graph.Nodes)
        {
            if (!seen.Add(node.Stem))
            {
                continue;
            }

            builder.Append("    ")
                .Append(MermaidId(node.Stem))
                .Append('[')
                .Append(SanitizeLabel(node.Stem))
                .AppendLine("]");
        }

        return builder.ToString();
    }

    private static string MermaidId(string stem)
    {
        var chars = (stem ?? string.Empty).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var id = new string(chars).Trim('_');
        if (id.Length == 0 || !char.IsLetter(id[0]))
        {
            id = "n" + id;
        }

        return id;
    }

    private static string SanitizeLabel(string stem)
        => (stem ?? string.Empty).Replace("[", string.Empty, StringComparison.Ordinal).Replace("]", string.Empty, StringComparison.Ordinal);

    public sealed record VaultStats(
        int Notes,
        int Words,
        int Tags,
        int Orphans,
        int OpenTasks,
        int Unresolved,
        int Daily);

    public static VaultStats Summarize(IEnumerable<string> folders)
    {
        var folderList = folders?.ToList() ?? [];
        var notes = 0;
        var words = 0;
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folderList))
        {
            notes++;
            if (!TryRead(path, out var text))
            {
                continue;
            }

            words += WordStats.CountWords(text);
            FrontMatter.TrySplit(text, out var fields, out _);
            foreach (var tag in MarkdownTags.Extract(text, fields))
            {
                tags.Add(tag);
            }
        }

        return new VaultStats(
            notes,
            words,
            tags.Count,
            Orphans(folderList).Count,
            OpenTasks(folderList).Count,
            Unresolved(folderList).Count,
            DailyNotes(folderList).Count);
    }

    public static IReadOnlyList<WikiBacklink> RelatedNotes(
        IEnumerable<string> folders,
        string stem,
        int maxHits = 40)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        stem = (stem ?? string.Empty).Trim();
        if (stem.Length == 0)
        {
            return [];
        }

        var path = Resolve(stem, null, folders);
        if (path is null || !TryRead(path, out var text))
        {
            return [];
        }

        FrontMatter.TrySplit(text, out var fields, out _);
        var tags = new HashSet<string>(MarkdownTags.Extract(text, fields), StringComparer.OrdinalIgnoreCase);
        if (tags.Count == 0)
        {
            return [];
        }

        var ranked = new List<(WikiBacklink Hit, int Score)>();
        foreach (var other in MarkdownPaths(folders))
        {
            if (string.Equals(other, path, StringComparison.OrdinalIgnoreCase) || !TryRead(other, out var otherText))
            {
                continue;
            }

            FrontMatter.TrySplit(otherText, out var otherFields, out _);
            var otherTags = MarkdownTags.Extract(otherText, otherFields);
            var score = otherTags.Count(tag => tags.Contains(tag));
            if (score == 0)
            {
                continue;
            }

            var shared = string.Join(", ", otherTags.Where(tag => tags.Contains(tag)));
            ranked.Add((new WikiBacklink(other, Path.GetFileName(other), 1, shared), score));
        }

        return ranked
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Hit.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxHits)
            .Select(item => item.Hit)
            .ToList();
    }

    public static IReadOnlyList<WikiBacklink> Untagged(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            FrontMatter.TrySplit(text, out var fields, out _);
            if (MarkdownTags.Extract(text, fields).Count > 0)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> EmptyNotes(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !IsEmptyNote(text))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static bool IsEmptyNote(string markdown)
    {
        FrontMatter.TrySplit(markdown ?? string.Empty, out _, out var body);
        var remaining = string.IsNullOrWhiteSpace(body) ? (markdown ?? string.Empty).Trim() : body.Trim();
        if (remaining.Length == 0)
        {
            return true;
        }

        var map = new LineMap(remaining);
        for (var i = 0; i < map.LineCount; i++)
        {
            var line = map.LineText(i).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!TryAtxHeading(line, out _, out _))
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<WikiBacklink> StaleNotes(
        IEnumerable<string> folders,
        int days = 30,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        days = Math.Clamp(days, 1, 3650);
        var cutoff = DateTime.UtcNow.AddDays(-days);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            DateTime write;
            try
            {
                write = File.GetLastWriteTimeUtc(path);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (write > cutoff)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, write.ToString("u")));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static int RewriteTag(
        IEnumerable<string> folders, string oldTag, string newTag, IEnumerable<string>? skipPaths = null)
    {
        oldTag = (oldTag ?? string.Empty).Trim().TrimStart('#');
        newTag = (newTag ?? string.Empty).Trim().TrimStart('#');
        if (oldTag.Length == 0 ||
            newTag.Length == 0 ||
            string.Equals(oldTag, newTag, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var count = 0;
        var skip = new HashSet<string>(skipPaths ?? [], StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            if (!seen.Add(path) || skip.Contains(path) || !TryRead(path, out var text))
            {
                continue;
            }

            var rewritten = MarkdownTags.Rewrite(text, oldTag, newTag);
            if (rewritten == text)
            {
                continue;
            }

            try
            {
                DocumentStore.WriteAtomic(path, rewritten);
                count++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return count;
    }

    public static string GenerateMoc(IEnumerable<string> folders, string? title = null)
    {
        var heading = string.IsNullOrWhiteSpace(title) ? "MOC" : title.Trim();
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("# " + heading);
        builder.AppendLine();
        var byLetter = new SortedDictionary<char, List<string>>();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (stem.Length == 0)
            {
                continue;
            }

            var letter = char.ToUpperInvariant(stem[0]);
            if (!char.IsLetter(letter))
            {
                letter = '#';
            }

            if (!byLetter.TryGetValue(letter, out var list))
            {
                list = [];
                byLetter[letter] = list;
            }

            list.Add(stem);
        }

        foreach (var pair in byLetter)
        {
            builder.Append("## ").Append(pair.Key).AppendLine();
            builder.AppendLine();
            foreach (var stem in pair.Value.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("- [[").Append(stem).AppendLine("]]");
            }

            builder.AppendLine();
        }

        var tags = AllTags(folders, 40);
        if (tags.Count > 0)
        {
            builder.AppendLine("## Tags");
            builder.AppendLine();
            foreach (var tag in tags)
            {
                builder.Append("- #").AppendLine(tag);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static string ToGraphJson(WikiGraph graph)
    {
        var nodes = (graph?.Nodes ?? []).Select(node =>
            "{\"stem\":" + JsonString(node.Stem) +
            ",\"outgoing\":" + node.Outgoing +
            ",\"incoming\":" + node.Incoming + "}");
        var edges = (graph?.Edges ?? []).Select(edge =>
            "{\"from\":" + JsonString(edge.FromStem) +
            ",\"to\":" + JsonString(edge.ToStem) +
            ",\"resolved\":" + (edge.Resolved ? "true" : "false") + "}");
        return "{\"nodes\":[" + string.Join(',', nodes) + "],\"edges\":[" + string.Join(',', edges) + "]}";
    }

    private static string JsonString(string value)
        => System.Text.Json.JsonSerializer.Serialize(value ?? string.Empty);

    public static IReadOnlyList<WikiBacklink> ModifiedToday(IEnumerable<string> folders, DateTime? day = null, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var date = (day ?? DateTime.Now).Date;
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            DateTime write;
            try
            {
                write = File.GetLastWriteTime(path);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            if (write.Date != date)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, write.ToString("HH:mm")));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> MatchingTitle(IEnumerable<string> folders, string query, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return [];
        }

        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            var title = NoteTitle(path);
            if (stem.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, title));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> LargestNotes(IEnumerable<string> folders, int maxHits = 40)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var ranked = new List<(string Path, long Size)>();
        foreach (var path in MarkdownPaths(folders))
        {
            try
            {
                ranked.Add((path, new FileInfo(path).Length));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return ranked
            .OrderByDescending(item => item.Size)
            .Take(maxHits)
            .Select(item => new WikiBacklink(item.Path, Path.GetFileName(item.Path), 1, item.Size + " B"))
            .ToList();
    }

    public static IReadOnlyList<WikiBacklink> WordiestNotes(IEnumerable<string> folders, int maxHits = 40)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var ranked = new List<(string Path, int Words)>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            ranked.Add((path, WordStats.CountWords(text)));
        }

        return ranked
            .OrderByDescending(item => item.Words)
            .Take(maxHits)
            .Select(item => new WikiBacklink(item.Path, Path.GetFileName(item.Path), 1, item.Words + " w"))
            .ToList();
    }

    public static IReadOnlyList<WikiBacklink> ShortestNotes(IEnumerable<string> folders, int maxHits = 40)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var ranked = new List<(string Path, int Words)>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            ranked.Add((path, WordStats.CountWords(text)));
        }

        return ranked
            .OrderBy(item => item.Words)
            .ThenBy(item => Path.GetFileName(item.Path), StringComparer.OrdinalIgnoreCase)
            .Take(maxHits)
            .Select(item => new WikiBacklink(item.Path, Path.GetFileName(item.Path), 1, item.Words + " w"))
            .ToList();
    }

    public static IReadOnlyList<WikiBacklink> NotesWithCitations(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var keys = MarkdownCitations.Keys(text);
            if (keys.Count == 0)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "[@" + keys[0] + "]"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithMath(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || text.IndexOf('$') < 0)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithFence(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var language = string.Empty;
            var found = false;
            var map = new LineMap(text);
            for (var i = 0; i < map.LineCount; i++)
            {
                var trimmed = map.LineText(i).TrimStart();
                if (!trimmed.StartsWith("```", StringComparison.Ordinal) &&
                    !trimmed.StartsWith("~~~", StringComparison.Ordinal))
                {
                    continue;
                }

                found = true;
                language = trimmed.TrimStart('`', '~').Trim();
                break;
            }

            if (!found)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, language.Length == 0 ? "fence" : language));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithMermaid(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithFenceLanguage(folders, ["mermaid"], maxHits);

    public static IReadOnlyList<WikiBacklink> NotesWithPlantuml(IEnumerable<string> folders, int maxHits = 80)
        => NotesWithFenceLanguage(folders, ["plantuml", "puml", "uml"], maxHits);

    private static IReadOnlyList<WikiBacklink> NotesWithFenceLanguage(
        IEnumerable<string> folders,
        string[] languages,
        int maxHits)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !HasFenceLanguage(text, languages))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, languages[0]));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    private static bool HasFenceLanguage(string text, string[] languages)
    {
        var map = new LineMap(text);
        for (var i = 0; i < map.LineCount; i++)
        {
            var trimmed = map.LineText(i).TrimStart();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal) &&
                !trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                continue;
            }

            var language = trimmed.TrimStart('`', '~').Trim();
            var token = language.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            foreach (var item in languages)
            {
                if (string.Equals(token, item, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithCallouts(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            var preview = string.Empty;
            for (var i = 0; i < map.LineCount; i++)
            {
                var trimmed = map.LineText(i).TrimStart();
                if (trimmed.StartsWith("> [!", StringComparison.Ordinal) ||
                    trimmed.StartsWith(":::", StringComparison.Ordinal))
                {
                    preview = trimmed.TrimStart('>', ':', ' ', '[', '!');
                    var end = preview.IndexOfAny([']', ' ']);
                    if (end > 0)
                    {
                        preview = preview[..end];
                    }

                    hits.Add(new WikiBacklink(path, Path.GetFileName(path), i + 1, preview.Length == 0 ? "callout" : preview));
                    break;
                }
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithEmbeds(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            foreach (var hit in WikiLinks.Find(text))
            {
                if (!hit.IsEmbed)
                {
                    continue;
                }

                var map = new LineMap(text);
                hits.Add(new WikiBacklink(
                    path,
                    Path.GetFileName(path),
                    map.LineOfOffset(hit.Start) + 1,
                    "![[" + hit.Target + "]]"));
                break;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithFootnotes(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || text.IndexOf("[^", StringComparison.Ordinal) < 0)
            {
                continue;
            }

            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    text,
                    @"\[\^[^\]]+\]",
                    System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromMilliseconds(80));
                if (!match.Success)
                {
                    continue;
                }

                var map = new LineMap(text);
                hits.Add(new WikiBacklink(path, Path.GetFileName(path), map.LineOfOffset(match.Index) + 1, match.Value));
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                continue;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithDraft(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) ||
                !FrontMatter.TrySplit(text, out var fields, out _) ||
                !fields.TryGetValue("draft", out var raw) ||
                !IsTruthy(raw))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "draft"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> TemplateNotes(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!IsTemplatePath(path.Replace('\\', '/')))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "_templates"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    private static bool IsTemplatePath(string normalized)
        => normalized.Contains("/_templates/", StringComparison.OrdinalIgnoreCase) ||
           normalized.EndsWith("/_templates", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<WikiBacklink> NotesWithYamlKey(
        IEnumerable<string> folders,
        string key,
        int maxHits = 80)
    {
        key = (key ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return [];
        }

        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) ||
                !FrontMatter.TrySplit(text, out var fields, out _) ||
                !fields.ContainsKey(key))
            {
                continue;
            }

            fields.TryGetValue(key, out var value);
            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, key + ": " + (value ?? string.Empty)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithWikilinks(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            foreach (var hit in WikiLinks.Find(text))
            {
                if (hit.IsEmbed)
                {
                    continue;
                }

                var map = new LineMap(text);
                hits.Add(new WikiBacklink(
                    path,
                    Path.GetFileName(path),
                    map.LineOfOffset(hit.Start) + 1,
                    "[[" + hit.Target + "]]"));
                break;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithoutWikilinks(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || WikiLinks.Find(text).Any(static hit => !hit.IsEmbed))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "nowiki"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithUrls(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || text.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                continue;
            }

            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    text,
                    @"https?://[^\s)>\]]+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(80));
                if (!match.Success)
                {
                    continue;
                }

                var map = new LineMap(text);
                hits.Add(new WikiBacklink(path, Path.GetFileName(path), map.LineOfOffset(match.Index) + 1, match.Value));
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                continue;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> DuplicateIds(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var groups = new Dictionary<string, List<(string Path, string Id)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !FrontMatter.TrySplit(text, out var fields, out _))
            {
                continue;
            }

            var id = FrontMatter.Id(fields);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!groups.TryGetValue(id, out var list))
            {
                list = [];
                groups[id] = list;
            }

            list.Add((path, id));
        }

        var hits = new List<WikiBacklink>();
        foreach (var pair in groups.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            foreach (var item in pair.Value)
            {
                hits.Add(new WikiBacklink(item.Path, Path.GetFileName(item.Path), 1, item.Id + " ×" + pair.Value.Count));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> PublishedNotes(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) ||
                !FrontMatter.TrySplit(text, out var fields, out _) ||
                (fields.TryGetValue("draft", out var raw) && IsTruthy(raw)))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "published"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithFixme(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var fences = FenceFold.Ranges(text);
            var map = new LineMap(text);
            var found = false;
            for (var line = 0; line < map.LineCount; line++)
            {
                if (FenceFold.At(fences, line) is { } fence && line > fence.StartLine && line < fence.EndLine)
                {
                    continue;
                }

                try
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        map.LineText(line),
                        @"\b(TODO|FIXME|XXX|HACK)\b",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(40));
                    if (!match.Success)
                    {
                        continue;
                    }

                    hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, match.Value));
                    found = true;
                    break;
                }
                catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
                {
                }
            }

            if (!found)
            {
                continue;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithDetails(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var at = text.IndexOf("<details>", StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                continue;
            }

            var map = new LineMap(text);
            hits.Add(new WikiBacklink(path, Path.GetFileName(path), map.LineOfOffset(at) + 1, "<details>"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithSetext(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            for (var line = 1; line < map.LineCount; line++)
            {
                var underline = map.LineText(line).Trim();
                if (underline.Length < 3 ||
                    !(underline.All(static c => c == '=') || underline.All(static c => c == '-')))
                {
                    continue;
                }

                var title = map.LineText(line - 1).Trim();
                if (title.Length == 0 || title[0] is '#' or '|' or '>' or '-' or '*')
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line, title));
                break;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithHtml(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var at = IndexOfNonDetailsHtml(text);
            if (at < 0)
            {
                continue;
            }

            var map = new LineMap(text);
            hits.Add(new WikiBacklink(path, Path.GetFileName(path), map.LineOfOffset(at) + 1, text.Substring(at, Math.Min(24, text.Length - at))));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    private static int IndexOfNonDetailsHtml(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            var at = text.IndexOf('<', i);
            if (at < 0)
            {
                return -1;
            }

            var j = at + 1;
            if (j < text.Length && text[j] == '/')
            {
                j++;
            }

            var start = j;
            while (j < text.Length && char.IsLetter(text[j]))
            {
                j++;
            }

            if (j > start)
            {
                var name = text[start..j];
                if (!name.Equals("details", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("summary", StringComparison.OrdinalIgnoreCase))
                {
                    return at;
                }
            }

            i = at + 1;
        }

        return -1;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithTables(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            for (var line = 0; line < map.LineCount; line++)
            {
                var raw = map.LineText(line).Trim();
                if (!raw.StartsWith('|') || raw.Count(static ch => ch == '|') < 2)
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, raw));
                break;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithComments(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var at = text.IndexOf("<!--", StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            var map = new LineMap(text);
            hits.Add(new WikiBacklink(path, Path.GetFileName(path), map.LineOfOffset(at) + 1, "<!--"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithoutTasks(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || HasTaskLine(text))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithBlockIds(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var fences = FenceFold.Ranges(text);
            var map = new LineMap(text);
            for (var line = 0; line < map.LineCount; line++)
            {
                if (FenceFold.At(fences, line) is { } fence && line > fence.StartLine && line < fence.EndLine)
                {
                    continue;
                }

                var raw = map.LineText(line);
                if (!TryBlockId(raw, out var id))
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line + 1, "^" + id));
                break;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithDefinitionLists(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var map = new LineMap(text);
            for (var line = 1; line < map.LineCount; line++)
            {
                var def = map.LineText(line).TrimStart();
                if (!def.StartsWith(": ", StringComparison.Ordinal) || def.Length < 3)
                {
                    continue;
                }

                var term = map.LineText(line - 1).Trim();
                if (term.Length == 0 || term.StartsWith(':') || term.StartsWith('#') || term.StartsWith('|'))
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), line, term));
                break;
            }

            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithMarkdownImages(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            var at = text.IndexOf("![", StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            var close = text.IndexOf("](", at, StringComparison.Ordinal);
            if (close < 0)
            {
                continue;
            }

            var map = new LineMap(text);
            hits.Add(new WikiBacklink(path, Path.GetFileName(path), map.LineOfOffset(at) + 1, text.Substring(at, Math.Min(32, text.Length - at))));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> DuplicateBlockIds(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var groups = new Dictionary<string, List<(string Path, int Line, string Id)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            foreach (var (line, id) in BlockIdsIn(text))
            {
                if (!groups.TryGetValue(id, out var list))
                {
                    list = [];
                    groups[id] = list;
                }

                list.Add((path, line, id));
            }
        }

        var hits = new List<WikiBacklink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in groups.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            foreach (var item in pair.Value)
            {
                if (!seen.Add(item.Path))
                {
                    continue;
                }

                hits.Add(new WikiBacklink(item.Path, Path.GetFileName(item.Path), item.Line + 1, "^" + item.Id + " ×" + pair.Value.Count));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithHr(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !HasStandaloneHr(text))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "---"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithoutImages(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || text.IndexOf("![", StringComparison.Ordinal) >= 0)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithoutHeadings(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || HasHeading(text))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithSlides(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || CountStandaloneHr(text) < 2)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "slides"));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> DuplicateAliases(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !FrontMatter.TrySplit(text, out var fields, out _))
            {
                continue;
            }

            foreach (var alias in FrontMatter.Aliases(fields))
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                if (!groups.TryGetValue(alias, out var list))
                {
                    list = [];
                    groups[alias] = list;
                }

                if (!list.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(path);
                }
            }
        }

        var hits = new List<WikiBacklink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in groups.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            foreach (var path in pair.Value)
            {
                if (!seen.Add(path))
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, pair.Key + " ×" + pair.Value.Count));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithoutAliases(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) ||
                !FrontMatter.TrySplit(text, out var fields, out _) ||
                FrontMatter.Aliases(fields).Count > 0)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> DuplicateDescriptions(IEnumerable<string> folders, int maxHits = 80)
        => DuplicateField(folders, "description", maxHits);

    public static IReadOnlyList<WikiBacklink> DuplicateCovers(IEnumerable<string> folders, int maxHits = 80)
        => DuplicateField(folders, "cover", maxHits);

    public static IReadOnlyList<WikiBacklink> DuplicateCategories(IEnumerable<string> folders, int maxHits = 80)
        => DuplicateField(folders, "category", maxHits);

    private static IReadOnlyList<WikiBacklink> DuplicateField(IEnumerable<string> folders, string field, int maxHits)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) ||
                !FrontMatter.TrySplit(text, out var fields, out _) ||
                !fields.TryGetValue(field, out var raw))
            {
                continue;
            }

            var value = (raw ?? string.Empty).Trim().Trim('"', '\'');
            if (value.Length == 0)
            {
                continue;
            }

            if (!groups.TryGetValue(value, out var list))
            {
                list = [];
                groups[value] = list;
            }

            if (!list.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(path);
            }
        }

        var hits = new List<WikiBacklink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in groups.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            foreach (var path in pair.Value)
            {
                if (!seen.Add(path))
                {
                    continue;
                }

                hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, pair.Key));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    private static IEnumerable<(int Line, string Id)> BlockIdsIn(string text)
    {
        var fences = FenceFold.Ranges(text);
        var map = new LineMap(text);
        for (var line = 0; line < map.LineCount; line++)
        {
            if (FenceFold.At(fences, line) is { } fence && line > fence.StartLine && line < fence.EndLine)
            {
                continue;
            }

            if (!TryBlockId(map.LineText(line), out var id))
            {
                continue;
            }

            yield return (line, id);
        }
    }

    private static bool TryBlockId(string line, out string id)
    {
        id = string.Empty;
        var at = line.LastIndexOf(" ^", StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        id = line[(at + 2)..].Trim();
        return id.Length > 0 && id[0] != '^' && id.All(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-');
    }

    private static bool ContainsBlockId(string markdown, string id)
    {
        id = (id ?? string.Empty).Trim().TrimStart('^');
        return id.Length > 0 &&
               BlockIdsIn(markdown).Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasHeading(string markdown)
    {
        return TocExtractor.Extract(markdown).Count > 0;
    }

    private static bool HasStandaloneHr(string markdown)
        => CountStandaloneHr(markdown) > 0;

    private static int CountStandaloneHr(string markdown)
    {
        if (FrontMatter.TrySplit(markdown, out _, out var body))
        {
            markdown = body;
        }

        var fences = FenceFold.Ranges(markdown);
        var map = new LineMap(markdown);
        var count = 0;
        for (var line = 0; line < map.LineCount; line++)
        {
            if (FenceFold.At(fences, line) is { } fence && line > fence.StartLine && line < fence.EndLine)
            {
                continue;
            }

            var trimmed = map.LineText(line).Trim();
            if (trimmed.Length < 3)
            {
                continue;
            }

            var mark = trimmed[0];
            if (mark is not ('-' or '*' or '_') || !trimmed.All(ch => ch == mark))
            {
                continue;
            }

            if (line > 0 && map.LineText(line - 1).Trim().Length > 0)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool HasTaskLine(string text)
    {
        var fences = FenceFold.Ranges(text);
        var map = new LineMap(text);
        for (var line = 0; line < map.LineCount; line++)
        {
            if (FenceFold.At(fences, line) is { } fence && line > fence.StartLine && line < fence.EndLine)
            {
                continue;
            }

            var trimmed = map.LineText(line).TrimStart();
            if (trimmed.StartsWith("- [ ] ", StringComparison.Ordinal) ||
                trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("* [ ] ", StringComparison.Ordinal) ||
                trimmed.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTruthy(string? raw)
    {
        raw = (raw ?? string.Empty).Trim().Trim('"', '\'');
        return raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               raw == "1";
    }

    private static bool IsFalsy(string? raw)
    {
        raw = (raw ?? string.Empty).Trim().Trim('"', '\'');
        return raw.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("no", StringComparison.OrdinalIgnoreCase) ||
               raw.Equals("off", StringComparison.OrdinalIgnoreCase) ||
               raw == "0";
    }

    public static IReadOnlyList<WikiBacklink> MissingDaily(
        IEnumerable<string> folders,
        DateTime? month = null,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var day = month ?? DateTime.Today;
        var folderList = folders?.Where(folder => !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)).ToList()
                         ?? [];
        if (folderList.Count == 0)
        {
            return [];
        }

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folderList))
        {
            var stem = StemFromPath(path);
            if (IsDailyStem(stem))
            {
                existing.Add(stem);
            }
        }

        var root = folderList[0];
        var hits = new List<WikiBacklink>();
        var days = DateTime.DaysInMonth(day.Year, day.Month);
        for (var n = 1; n <= days; n++)
        {
            var stamp = new DateTime(day.Year, day.Month, n).ToString(
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture);
            if (existing.Contains(stamp))
            {
                continue;
            }

            hits.Add(new WikiBacklink(Path.Combine(root, stamp + ".md"), stamp + ".md", 1, stamp));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> WithoutFrontMatter(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || FrontMatter.TrySplit(text, out _, out _))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> MatchingPath(
        IEnumerable<string> folders,
        string query,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return [];
        }

        var folderList = folders?.ToList() ?? [];
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folderList))
        {
            var relative = RelativeToVault(path, folderList) ?? Path.GetFileName(path);
            if (!relative.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !path.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, relative.Replace('\\', '/')));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithId(
        IEnumerable<string> folders,
        string? query = null,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        query = (query ?? string.Empty).Trim();
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !FrontMatter.TrySplit(text, out var fields, out _))
            {
                continue;
            }

            var id = FrontMatter.Id(fields);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (query.Length > 0 &&
                !string.Equals(id, query, StringComparison.OrdinalIgnoreCase) &&
                !id.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, id));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> WithoutId(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            FrontMatter.TrySplit(text, out var fields, out _);
            if (FrontMatter.Id(fields) is not null)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> WithoutTitle(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text))
            {
                continue;
            }

            FrontMatter.TrySplit(text, out var fields, out _);
            if (fields.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> NotesWithDateField(
        IEnumerable<string> folders,
        string key,
        string? stamp = null,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        key = (key ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return [];
        }

        stamp = string.IsNullOrWhiteSpace(stamp)
            ? DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : stamp.Trim();
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            if (!TryRead(path, out var text) || !FrontMatter.TrySplit(text, out var fields, out _))
            {
                continue;
            }

            if (!fields.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            raw = raw.Trim();
            if (!raw.StartsWith(stamp, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, key + ": " + raw));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> ArchivedNotes(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folderList))
        {
            if (!VaultAttachments.IsArchived(path, folderList))
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, VaultAttachments.ArchiveFolderName));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> Calendar(
        IEnumerable<string> folders,
        int year,
        int month,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        month = Math.Clamp(month, 1, 12);
        var hits = new List<WikiBacklink>();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (!DateTime.TryParseExact(
                    stem,
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var day) ||
                day.Year != year ||
                day.Month != month)
            {
                continue;
            }

            hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, stem));
            if (hits.Count >= maxHits)
            {
                return hits;
            }
        }

        return hits;
    }

    public static string QueryTable(IEnumerable<string> folders, string? columns = null)
    {
        var cols = (columns ?? "title,tags")
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(column => column.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();
        if (cols.Count == 0)
        {
            cols.Add("title");
        }

        var builder = new System.Text.StringBuilder();
        builder.Append("| Note |");
        foreach (var column in cols)
        {
            builder.Append(' ').Append(column).Append(" |");
        }

        builder.AppendLine();
        builder.Append("| --- |");
        foreach (var _ in cols)
        {
            builder.Append(" --- |");
        }

        builder.AppendLine();
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (stem.Length == 0)
            {
                continue;
            }

            TryRead(path, out var text);
            FrontMatter.TrySplit(text ?? string.Empty, out var fields, out _);
            builder.Append("| [[").Append(stem).Append("]] |");
            foreach (var column in cols)
            {
                var cell = CellValue(fields, column, stem);
                builder.Append(' ').Append(EscapeTableCell(cell)).Append(" |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static string FormatBacklinks(IReadOnlyList<WikiBacklink> links)
    {
        if (links is null || links.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var link in links)
        {
            var stem = StemFromPath(link.Path);
            if (stem.Length == 0)
            {
                continue;
            }

            builder.Append("- [[").Append(stem).Append("]]");
            if (!string.IsNullOrWhiteSpace(link.Preview) &&
                !link.Preview.Contains("[[" + stem + "]]", StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(" — ").Append(link.Preview.Trim());
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    public static IReadOnlyList<WikiBacklink> DuplicateStems(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            var stem = StemFromPath(path);
            if (stem.Length == 0)
            {
                continue;
            }

            if (!groups.TryGetValue(stem, out var list))
            {
                list = [];
                groups[stem] = list;
            }

            list.Add(path);
        }

        var hits = new List<WikiBacklink>();
        foreach (var pair in groups.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            foreach (var path in pair.Value)
            {
                hits.Add(new WikiBacklink(path, Path.GetFileName(path), 1, "×" + pair.Value.Count));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> DuplicateTitles(IEnumerable<string> folders, int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        var groups = new Dictionary<string, List<(string Path, string Title)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in MarkdownPaths(folders))
        {
            var title = NoteTitle(path);
            if (title.Length == 0)
            {
                continue;
            }

            if (!groups.TryGetValue(title, out var list))
            {
                list = [];
                groups[title] = list;
            }

            list.Add((path, title));
        }

        var hits = new List<WikiBacklink>();
        foreach (var pair in groups.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (pair.Value.Count < 2)
            {
                continue;
            }

            foreach (var item in pair.Value)
            {
                hits.Add(new WikiBacklink(item.Path, Path.GetFileName(item.Path), 1, item.Title));
                if (hits.Count >= maxHits)
                {
                    return hits;
                }
            }
        }

        return hits;
    }

    public static IReadOnlyList<WikiBacklink> Dataview(
        IEnumerable<string> folders,
        string query,
        int maxHits = 80)
    {
        maxHits = Math.Clamp(maxHits, 1, 200);
        query = (query ?? string.Empty).Trim();
        if (query.Length == 0)
        {
            return [];
        }

        query = StripDataviewClauses(query, out var sortKey, out var sortDesc, out var parsedLimit);
        if (parsedLimit > 0)
        {
            maxHits = Math.Clamp(parsedLimit, 1, 200);
        }

        var tag = string.Empty;
        var where = string.Empty;
        var remaining = query;
        var from = remaining.IndexOf("FROM ", StringComparison.OrdinalIgnoreCase);
        var whereAt = remaining.IndexOf("WHERE ", StringComparison.OrdinalIgnoreCase);
        if (from < 0 && whereAt < 0 && TryParseFrontMatterClause(query, out _, out _, out _))
        {
            var hits = QueryFrontMatter(folders, query, 200);
            if (sortKey.Length > 0)
            {
                hits = SortHits(hits, sortKey, sortDesc);
            }

            return hits.Take(maxHits).ToList();
        }

        if (from >= 0)
        {
            var start = from + 5;
            var end = whereAt > from ? whereAt : remaining.Length;
            tag = remaining[start..end].Trim();
        }

        if (whereAt >= 0)
        {
            where = remaining[(whereAt + 6)..].Trim();
        }

        var fromTags = tag
            .Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(piece => piece.TrimStart('#'))
            .Where(piece => piece.Length > 0)
            .ToList();
        IReadOnlyList<WikiBacklink> source = fromTags.Count > 0
            ? FilesWithAllTags(folders, fromTags, maxHits: 200)
            : MarkdownPaths(folders)
                .Select(path => new WikiBacklink(path, Path.GetFileName(path), 1, StemFromPath(path)))
                .ToList();

        if (where.Length > 0)
        {
            var matched = new HashSet<string>(
                QueryFrontMatter(folders, where, 200).Select(hit => hit.Path),
                StringComparer.OrdinalIgnoreCase);
            source = source.Where(hit => matched.Contains(hit.Path)).ToList();
        }

        if (sortKey.Length > 0)
        {
            source = SortHits(source, sortKey, sortDesc);
        }

        return source.Take(maxHits).ToList();
    }

    public static string FormatCalendar(IEnumerable<string> folders, int year, int month)
    {
        month = Math.Clamp(month, 1, 12);
        var notes = new HashSet<string>(
            Calendar(folders, year, month).Select(hit => hit.Preview),
            StringComparer.OrdinalIgnoreCase);
        var first = new DateTime(year, month, 1);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("# " + first.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine("| Mon | Tue | Wed | Thu | Fri | Sat | Sun |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var days = DateTime.DaysInMonth(year, month);
        var day = 1;
        while (day <= days)
        {
            builder.Append('|');
            for (var col = 0; col < 7; col++)
            {
                if ((day == 1 && col < offset) || day > days)
                {
                    builder.Append("   |");
                    continue;
                }

                var stamp = first.AddDays(day - 1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                if (notes.Contains(stamp))
                {
                    builder.Append(" [[").Append(stamp).Append("]] |");
                }
                else
                {
                    builder.Append(' ').Append(day.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(" |");
                }

                day++;
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static bool LocalFileExists(string url, string documentPath)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            if (Path.IsPathRooted(url))
            {
                return File.Exists(url);
            }

            var root = Path.GetDirectoryName(documentPath);
            if (string.IsNullOrEmpty(root))
            {
                return File.Exists(url);
            }

            return File.Exists(Path.GetFullPath(Path.Combine(root, url.Replace('/', Path.DirectorySeparatorChar))));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string StripDataviewClauses(string query, out string sortKey, out bool sortDesc, out int limit)
    {
        sortKey = string.Empty;
        sortDesc = false;
        limit = 0;
        query ??= string.Empty;
        try
        {
            var removals = new List<(int Index, int Length)>();
            var limitMatch = System.Text.RegularExpressions.Regex.Match(
                query,
                @"\bLIMIT\s+(\d+)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(80));
            if (limitMatch.Success && int.TryParse(limitMatch.Groups[1].Value, out var parsed))
            {
                limit = parsed;
                removals.Add((limitMatch.Index, limitMatch.Length));
            }

            var sortMatch = System.Text.RegularExpressions.Regex.Match(
                query,
                @"\bSORT\s+([A-Za-z][\w]*)(?:\s+(ASC|DESC))?\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(80));
            if (sortMatch.Success)
            {
                sortKey = sortMatch.Groups[1].Value;
                sortDesc = sortMatch.Groups[2].Value.Equals("DESC", StringComparison.OrdinalIgnoreCase);
                removals.Add((sortMatch.Index, sortMatch.Length));
            }

            foreach (var removal in removals.OrderByDescending(item => item.Index))
            {
                query = query.Remove(removal.Index, removal.Length);
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
        }

        return query.Trim();
    }

    private static IReadOnlyList<WikiBacklink> SortHits(
        IReadOnlyList<WikiBacklink> source,
        string sortKey,
        bool descending)
    {
        var keyed = new List<(WikiBacklink Hit, string Key)>(source.Count);
        foreach (var hit in source)
        {
            var key = hit.Preview;
            if (TryRead(hit.Path, out var text) && FrontMatter.TrySplit(text, out var fields, out _))
            {
                key = CellValue(fields, sortKey, StemFromPath(hit.Path));
            }

            keyed.Add((hit, key ?? string.Empty));
        }

        var ordered = descending
            ? keyed.OrderByDescending(item => item.Key, StringComparer.OrdinalIgnoreCase)
            : keyed.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase);
        return ordered.Select(item => item.Hit).ToList();
    }

    private static IEnumerable<string> MarkdownPaths(IEnumerable<string> folders)
        => WorkspaceScanner.EnumerateMarkdownPaths(folders);

    private static bool TryRead(string path, out string text)
        => WorkspaceScanner.TryReadText(path, out text);

    private static bool IsBacklinkTo(
        string linkTarget,
        string stem,
        string? notePath,
        string fromPath,
        IReadOnlyList<string> folders)
    {
        if (notePath is null)
        {
            return string.Equals(linkTarget, stem, StringComparison.OrdinalIgnoreCase);
        }

        var resolved = Resolve(linkTarget, fromPath, folders);
        return resolved is not null &&
               string.Equals(resolved, notePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string? RelativeToVault(string path, IReadOnlyList<string> folders)
    {
        var vault = VaultAttachments.ResolveVaultRoot(path, folders);
        if (string.IsNullOrEmpty(vault))
        {
            return null;
        }

        try
        {
            return Path.GetRelativePath(vault, path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string CellValue(IReadOnlyDictionary<string, string> fields, string column, string stem)
    {
        if (column.Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            return fields.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title)
                ? title
                : stem;
        }

        if (column.Equals("file", StringComparison.OrdinalIgnoreCase) ||
            column.Equals("note", StringComparison.OrdinalIgnoreCase))
        {
            return stem;
        }

        return fields.TryGetValue(column, out var value) ? value : string.Empty;
    }

    private static string EscapeTableCell(string value)
        => (value ?? string.Empty)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}

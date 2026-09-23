using MarkdownMkII.Core.Services;

namespace MarkdownMkII.Core.Text;

public sealed record MarkdownIssue(int Line, string Code, string Message);

public sealed record VaultIssue(string Path, MarkdownIssue Issue);

public static class MarkdownDiagnostics
{
    public static IReadOnlyList<MarkdownIssue> Analyze(
        string markdown,
        string? documentPath = null,
        IEnumerable<string>? vaultFolders = null)
    {
        markdown ??= string.Empty;
        if (markdown.Length == 0)
        {
            return [];
        }

        var issues = new List<MarkdownIssue>();
        var map = new LineMap(markdown);
        AnalyzeFences(map, issues);
        AnalyzeEmptyLinks(markdown, map, issues);
        AnalyzeUnbalancedPairs(map, issues);
        AnalyzeTrailingWhitespace(map, issues);
        AnalyzeWikiAndImages(markdown, map, documentPath, vaultFolders, issues);
        AnalyzeEmptyAlts(markdown, map, issues);
        AnalyzeDuplicateHeadings(markdown, issues);
        AnalyzeSkippedHeadings(markdown, issues);
        return issues;
    }

    public static IReadOnlyList<VaultIssue> AnalyzeVault(IEnumerable<string> folders, int max = 80)
    {
        max = Math.Clamp(max, 1, 200);
        var folderList = folders?.ToList() ?? [];
        var issues = new List<VaultIssue>();
        foreach (var folder in folderList)
        {
            foreach (var path in WorkspaceScanner.EnumerateMarkdownFiles(folder))
            {
                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var issue in Analyze(text, path, folderList))
                {
                    issues.Add(new VaultIssue(path, issue));
                    if (issues.Count >= max)
                    {
                        return issues;
                    }
                }
            }
        }

        return issues;
    }

    private static void AnalyzeFences(LineMap map, List<MarkdownIssue> issues)
    {
        var openLine = -1;
        var fence = new MarkdownFence();
        for (var line = 0; line < map.LineCount; line++)
        {
            if (!fence.Advance(map.LineText(line)))
            {
                continue;
            }

            if (openLine < 0)
            {
                openLine = line;
            }
            else
            {
                openLine = -1;
            }
        }

        if (openLine >= 0)
        {
            issues.Add(new MarkdownIssue(openLine, "fence", "Blocco codice non chiuso"));
        }
    }

    private static void AnalyzeEmptyLinks(string markdown, LineMap map, List<MarkdownIssue> issues)
    {
        try
        {
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                         markdown,
                         @"\[\]\(\)|!\[\]\(\)",
                         System.Text.RegularExpressions.RegexOptions.None,
                         TimeSpan.FromMilliseconds(80)))
            {
                issues.Add(new MarkdownIssue(map.LineOfOffset(match.Index), "link", "Link o immagine vuoti"));
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
        }
    }

    private static void AnalyzeUnbalancedPairs(LineMap map, List<MarkdownIssue> issues)
    {
        var ticks = 0;
        var codeLines = MarkdownFence.CodeLines(map);
        for (var line = 0; line < map.LineCount; line++)
        {
            var text = map.LineText(line);
            if (codeLines[line])
            {
                continue;
            }

            foreach (var ch in text)
            {
                if (ch == '`')
                {
                    ticks++;
                }
            }
        }

        if (ticks % 2 != 0)
        {
            issues.Add(new MarkdownIssue(0, "code", "Virgolette di codice inline non bilanciate"));
        }
    }

    private static void AnalyzeTrailingWhitespace(LineMap map, List<MarkdownIssue> issues)
    {
        var fenceState = new MarkdownFence();
        for (var line = 0; line < map.LineCount; line++)
        {
            var text = map.LineText(line);
            var trimmedStart = text.TrimStart();
            if (fenceState.Advance(text))
            {
                continue;
            }

            if (fenceState.IsOpen || text.Length == 0)
            {
                continue;
            }

            if (text.EndsWith('\t') || (text.EndsWith(' ') && !text.EndsWith("  ", StringComparison.Ordinal)))
            {
                issues.Add(new MarkdownIssue(line, "whitespace", "Spazi finali"));
            }
        }
    }

    private static void AnalyzeWikiAndImages(
        string markdown,
        LineMap map,
        string? documentPath,
        IEnumerable<string>? vaultFolders,
        List<MarkdownIssue> issues)
    {
        foreach (var hit in Markdown.WikiLinks.Find(markdown))
        {
            if (string.IsNullOrWhiteSpace(documentPath) && (vaultFolders is null || !vaultFolders.Any()))
            {
                break;
            }

            var resolved = Markdown.WikiIndex.Resolve(hit.Target, documentPath, vaultFolders);
            if (resolved is null)
            {
                if (hit.Target.Length > 0)
                {
                    issues.Add(new MarkdownIssue(map.LineOfOffset(hit.Start), "wiki", "Wikilink non trovato: " + hit.Target));
                }
            }

            var heading = hit.Heading;
            if (string.IsNullOrEmpty(heading))
            {
                continue;
            }

            string? source = null;
            if (resolved is not null &&
                !string.IsNullOrWhiteSpace(documentPath) &&
                string.Equals(resolved, Path.GetFullPath(documentPath), StringComparison.OrdinalIgnoreCase))
            {
                source = markdown;
            }
            else if (resolved is not null)
            {
                try
                {
                    source = File.ReadAllText(resolved);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            else if (hit.Target.Length == 0)
            {
                source = markdown;
            }

            if (source is not null && !Markdown.WikiIndex.ContainsHeading(source, heading))
            {
                issues.Add(new MarkdownIssue(
                    map.LineOfOffset(hit.Start),
                    "heading",
                    "Titolo wikilink mancante: " + heading));
            }
        }

        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return;
        }

        try
        {
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                         markdown,
                         @"!\[([^\]]*)\]\(([^)]+)\)",
                         System.Text.RegularExpressions.RegexOptions.None,
                         TimeSpan.FromMilliseconds(80)))
            {
                var url = match.Groups[2].Value.Trim().Trim('"');
                if (url.Length == 0 ||
                    url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Preview.LinkResolver.TryResolveLocal(url, documentPath) is null)
                {
                    issues.Add(new MarkdownIssue(map.LineOfOffset(match.Index), "image", "Immagine locale mancante"));
                }
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
        }
    }

    private static void AnalyzeEmptyAlts(string markdown, LineMap map, List<MarkdownIssue> issues)
    {
        try
        {
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                         markdown,
                         @"!\[\]\([^)]+\)",
                         System.Text.RegularExpressions.RegexOptions.None,
                         TimeSpan.FromMilliseconds(80)))
            {
                issues.Add(new MarkdownIssue(map.LineOfOffset(match.Index), "alt", "Immagine senza testo alternativo"));
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
        }
    }

    private static void AnalyzeDuplicateHeadings(string markdown, List<MarkdownIssue> issues)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in TocExtractor.Extract(markdown))
        {
            if (seen.TryGetValue(node.Title, out _))
            {
                issues.Add(new MarkdownIssue(node.SourceLine, "heading", "Titolo duplicato: " + node.Title));
            }
            else
            {
                seen[node.Title] = node.SourceLine;
            }
        }
    }

    private static void AnalyzeSkippedHeadings(string markdown, List<MarkdownIssue> issues)
    {
        var last = 0;
        foreach (var node in TocExtractor.Extract(markdown))
        {
            if (last > 0 && node.Level > last + 1)
            {
                issues.Add(new MarkdownIssue(node.SourceLine, "heading-skip", "Livello titolo saltato: " + node.Title));
            }

            last = node.Level;
        }
    }
}

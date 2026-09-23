namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    public static string AsQuote(string text)
    {
        text ??= string.Empty;
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normalized.Length == 0)
        {
            return "> ";
        }

        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Length == 0 ? ">" : "> " + lines[i];
        }

        return string.Join('\n', lines);
    }

    public static EditResult UpsertHeadingSection(string text, string heading, string body, int level = 2)
    {
        text ??= string.Empty;
        heading = (heading ?? string.Empty).Trim();
        body ??= string.Empty;
        level = Math.Clamp(level, 1, 6);
        if (heading.Length == 0)
        {
            return new EditResult(text, 0, 0);
        }

        var prefix = new string('#', level) + " " + heading;
        var block = prefix + "\n\n" + body.TrimEnd() + "\n";
        var outline = TocExtractor.Extract(text);
        var node = outline.FirstOrDefault(item =>
            item.Level == level &&
            string.Equals(item.Title, heading, StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            var glue = text.Length == 0 ? string.Empty : text.EndsWith('\n') ? "\n" : "\n\n";
            var next = text + glue + block;
            return new EditResult(next, text.Length + glue.Length, 0);
        }

        var map = new LineMap(text);
        var start = map.OffsetOfLine(node.SourceLine);
        var end = map.OffsetOfLine(node.SourceEndLine + 1);
        return Replace(text, start, end - start, block, start, 0);
    }

    public static EditResult InsertWikiGraph(string text, int start, int length, string mermaid)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        mermaid = string.IsNullOrWhiteSpace(mermaid) ? "flowchart LR\n    empty[Vault]" : mermaid.Trim();
        var fence = "```mermaid\n" + mermaid + "\n```";
        var caret = start + "```mermaid\n".Length;
        return Replace(text, start, length, fence, caret, mermaid.Length);
    }

    public static EditResult InsertBlockId(string text, int start, int length, string? id = null)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N")[..6] : id.Trim().TrimStart('^');
        if (id.Length == 0)
        {
            return new EditResult(text, start, 0);
        }

        var map = new LineMap(text);
        if (map.LineCount == 0)
        {
            return Replace(text, 0, 0, " ^" + id, 1, id.Length + 1);
        }

        var line = map.LineOfOffset(Math.Clamp(start, 0, Math.Max(0, text.Length)));
        var lineText = map.LineText(line);
        if (System.Text.RegularExpressions.Regex.IsMatch(lineText, @"\s\^[A-Za-z0-9_-]+\s*$"))
        {
            return new EditResult(text, start, 0);
        }

        var span = map.LineSpan(line);
        var contentLength = lineText.Length;
        var next = lineText.TrimEnd() + " ^" + id;
        return Replace(text, span.Start, contentLength, next, span.Start + next.Length, 0);
    }

    public static EditResult? AssignMissingBlockIds(string text)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var lines = Lines(map);
        var fences = FenceFold.Ranges(text);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < lines.Count; i++)
        {
            if (FenceFold.At(fences, i) is { } fence && i > fence.StartLine && i < fence.EndLine)
            {
                continue;
            }

            var match = System.Text.RegularExpressions.Regex.Match(lines[i], @"\s\^([A-Za-z0-9_-]+)\s*$");
            if (match.Success)
            {
                used.Add(match.Groups[1].Value);
            }
        }

        var changed = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (FenceFold.At(fences, i) is { } fence && i > fence.StartLine && i < fence.EndLine)
            {
                continue;
            }

            var level = HeadingLevel(lines[i]);
            if (level == 0)
            {
                continue;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"\s\^[A-Za-z0-9_-]+\s*$"))
            {
                continue;
            }

            var title = StripHeading(lines[i]);
            var slug = HeadingAnchor(title);
            if (slug.Length == 0)
            {
                slug = "h" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var unique = slug;
            var n = 2;
            while (!used.Add(unique))
            {
                unique = slug + "-" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                n++;
            }

            lines[i] = lines[i].TrimEnd() + " ^" + unique;
            changed = true;
        }

        if (!changed)
        {
            return null;
        }

        return RebuildLines(lines, text, 0, 0);
    }

    public static EditResult? StripBlockIds(string text)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var lines = Lines(map);
        var fences = FenceFold.Ranges(text);
        var changed = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (FenceFold.At(fences, i) is { } fence && i > fence.StartLine && i < fence.EndLine)
            {
                continue;
            }

            var next = System.Text.RegularExpressions.Regex.Replace(
                lines[i],
                @"\s\^[A-Za-z0-9_-]+\s*$",
                string.Empty);
            if (!string.Equals(next, lines[i], StringComparison.Ordinal))
            {
                changed = true;
                lines[i] = next;
            }
        }

        if (!changed)
        {
            return null;
        }

        return RebuildLines(lines, text, 0, 0);
    }

    public static EditResult? UnwrapFence(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var fence = FenceFold.At(FenceFold.Ranges(text), line);
        if (fence is null || fence.Value.EndLine <= fence.Value.StartLine)
        {
            return null;
        }

        var lines = Lines(map);
        if (fence.Value.EndLine >= lines.Count)
        {
            return null;
        }

        lines.RemoveAt(fence.Value.EndLine);
        lines.RemoveAt(fence.Value.StartLine);
        return RebuildLines(lines, text, map.OffsetOfLine(fence.Value.StartLine), 0);
    }

    public static EditResult? SetFenceLanguage(string text, int start, string? language)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var fence = FenceFold.At(FenceFold.Ranges(text), line);
        if (fence is null)
        {
            return null;
        }

        var lines = Lines(map);
        var open = lines[fence.Value.StartLine];
        var indentLength = 0;
        while (indentLength < open.Length && (open[indentLength] == ' ' || open[indentLength] == '\t'))
        {
            indentLength++;
        }

        var trimmed = open[indentLength..];
        if (trimmed.Length < 3 || trimmed[0] is not ('~' or '`'))
        {
            return null;
        }

        var markerLength = 1;
        while (markerLength < trimmed.Length && trimmed[markerLength] == trimmed[0])
        {
            markerLength++;
        }

        var marker = trimmed[..markerLength];
        var next = open[..indentLength] + marker + (language ?? string.Empty).Trim();
        if (string.Equals(next, open, StringComparison.Ordinal))
        {
            return null;
        }

        lines[fence.Value.StartLine] = next;
        return RebuildLines(lines, text, map.OffsetOfLine(fence.Value.StartLine), next.Length);
    }

    public static EditResult? StripHtmlComments(string text)
    {
        text ??= string.Empty;
        var fences = FenceFold.Ranges(text);
        var map = new LineMap(text);
        var changed = false;
        string rebuilt;
        try
        {
            rebuilt = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"<!--[\s\S]*?-->",
                match =>
                {
                    var line = map.LineOfOffset(Math.Clamp(match.Index, 0, Math.Max(0, text.Length - 1)));
                    if (FenceFold.At(fences, line) is { } fence &&
                        line > fence.StartLine &&
                        line < fence.EndLine)
                    {
                        return match.Value;
                    }

                    changed = true;
                    return string.Empty;
                },
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(80));
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return null;
        }

        if (!changed)
        {
            return null;
        }

        return new EditResult(rebuilt, 0, 0);
    }

    public static EditResult StripFrontMatter(string text)
    {
        text ??= string.Empty;
        var stripped = FrontMatter.Strip(text);
        return new EditResult(stripped, 0, 0);
    }

    public readonly record struct ExtractNoteResult(
        string Text,
        string NoteBody,
        string Stem,
        int SelectionStart,
        int SelectionLength);

    public static ExtractNoteResult ExtractToNote(string text, int start, int length, string? title)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        if (length == 0)
        {
            var map = new LineMap(text);
            var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
            var range = TocExtractor.RangeAt(TocExtractor.Extract(text), line);
            if (range is not null)
            {
                start = map.OffsetOfLine(range.Value.StartLine);
                var end = map.OffsetOfLine(range.Value.EndLine + 1);
                length = Math.Max(0, end - start);
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = TocExtractor.Extract(text)
                        .FirstOrDefault(node => node.SourceLine == range.Value.StartLine)
                        ?.Title;
                }
            }
            else
            {
                (start, length) = CurrentWord(text, start);
            }
        }

        var body = text.Substring(start, length).Trim('\r', '\n');
        if (body.Length == 0)
        {
            return new ExtractNoteResult(text, string.Empty, string.Empty, start, 0);
        }

        title = (title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            var first = body.Split('\n')[0].Trim().TrimStart('#', ' ');
            title = first.Length == 0 ? "note" : first;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            title = title.Replace(invalid, '-');
        }

        var wiki = "[[" + title + "]]";
        var remaining = text[..start] + wiki + text[(start + length)..];
        var note = body.StartsWith('#') ? body + (body.EndsWith('\n') ? string.Empty : "\n") : "# " + title + "\n\n" + body + "\n";
        return new ExtractNoteResult(remaining, note, title, start, wiki.Length);
    }
}

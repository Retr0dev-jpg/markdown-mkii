namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    public static EditResult? MoveHeadingSection(string text, int start, int direction)
    {
        text ??= string.Empty;
        if (text.Length == 0 || direction == 0)
        {
            return null;
        }

        var outline = TocExtractor.Extract(text);
        if (outline.Count == 0)
        {
            return null;
        }

        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var range = TocExtractor.RangeAt(outline, line);
        if (range is null)
        {
            return null;
        }

        var current = outline.LastOrDefault(node => node.SourceLine == range.Value.StartLine);
        if (current is null)
        {
            return null;
        }

        var index = 0;
        for (var i = 0; i < outline.Count; i++)
        {
            if (outline[i].SourceLine == current.SourceLine && outline[i].Level == current.Level)
            {
                index = i;
                break;
            }
        }

        OutlineNode? sibling = null;
        if (direction < 0)
        {
            for (var i = index - 1; i >= 0; i--)
            {
                if (outline[i].Level < current.Level)
                {
                    break;
                }

                if (outline[i].Level == current.Level)
                {
                    sibling = outline[i];
                    break;
                }
            }
        }
        else
        {
            for (var i = index + 1; i < outline.Count; i++)
            {
                if (outline[i].Level < current.Level)
                {
                    break;
                }

                if (outline[i].Level == current.Level)
                {
                    sibling = outline[i];
                    break;
                }
            }
        }

        if (sibling is null)
        {
            return null;
        }

        var first = current.SourceLine <= sibling.SourceLine ? current : sibling;
        var second = current.SourceLine <= sibling.SourceLine ? sibling : current;
        var aStart = map.OffsetOfLine(first.SourceLine);
        var aEnd = map.OffsetOfLine(first.SourceEndLine + 1);
        var bStart = map.OffsetOfLine(second.SourceLine);
        var bEnd = map.OffsetOfLine(second.SourceEndLine + 1);
        if (aEnd > bStart)
        {
            return null;
        }

        var blockA = EnsureTrailingNewline(text[aStart..aEnd]);
        var blockB = EnsureTrailingNewline(text[bStart..bEnd]);
        var middle = text[aEnd..bStart];
        var rebuilt = text[..aStart] + blockB + middle + blockA + text[bEnd..];
        var caret = direction < 0 ? aStart : aStart + blockB.Length + middle.Length;
        return new EditResult(rebuilt, Math.Clamp(caret, 0, rebuilt.Length), 0);
    }

    public static EditResult? SelectHeadingSection(string text, int start)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        var outline = TocExtractor.Extract(text);
        if (outline.Count == 0)
        {
            return null;
        }

        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var range = TocExtractor.RangeAt(outline, line);
        if (range is null)
        {
            return null;
        }

        var from = map.OffsetOfLine(range.Value.StartLine);
        var to = map.OffsetOfLine(range.Value.EndLine + 1);
        return new EditResult(text, from, Math.Max(0, to - from));
    }

    public static EditResult? DuplicateHeadingSection(string text, int start)
    {
        var selected = SelectHeadingSection(text, start);
        if (selected is null || selected.Value.SelectionLength <= 0)
        {
            return null;
        }

        var block = text.Substring(selected.Value.SelectionStart, selected.Value.SelectionLength);
        if (!block.EndsWith('\n'))
        {
            block += "\n";
        }

        var insertAt = selected.Value.SelectionStart + selected.Value.SelectionLength;
        var rebuilt = text[..insertAt] + block + text[insertAt..];
        return new EditResult(rebuilt, insertAt, block.Length);
    }

    public static EditResult WrapDetails(string text, int start, int length, string? summary = null)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var selected = text.Substring(start, length).Trim('\r', '\n');
        if (selected.Length == 0)
        {
            selected = "…";
        }

        var title = (summary ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            var first = selected.Split('\n')[0].Trim().TrimStart('#', '-', '*', ' ');
            title = first.Length == 0 ? "Details" : first;
            if (title.Length > 80)
            {
                title = title[..80].Trim();
            }
        }

        var wrapped = "<details>\n<summary>" + title + "</summary>\n\n" + selected + "\n</details>\n";
        return Replace(text, start, length, wrapped, start, wrapped.Length);
    }

    public static EditResult? UnwrapDetails(string text, int start)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        start = Math.Clamp(start, 0, text.Length);
        var searchFrom = text.Length == 0 ? 0 : Math.Min(start, text.Length - 1);
        var open = text.LastIndexOf("<details>", searchFrom, StringComparison.OrdinalIgnoreCase);
        if (open < 0)
        {
            open = text.IndexOf("<details>", StringComparison.OrdinalIgnoreCase);
        }

        if (open < 0)
        {
            return null;
        }

        var close = text.IndexOf("</details>", open, StringComparison.OrdinalIgnoreCase);
        if (close < 0)
        {
            return null;
        }

        var innerStart = open + "<details>".Length;
        var inner = text[innerStart..close];
        var summaryOpen = inner.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
        var summaryClose = inner.IndexOf("</summary>", StringComparison.OrdinalIgnoreCase);
        if (summaryOpen >= 0 && summaryClose > summaryOpen)
        {
            inner = inner[(summaryClose + "</summary>".Length)..];
        }

        inner = inner.Trim('\r', '\n');
        if (inner.Length > 0)
        {
            inner += "\n";
        }

        var end = close + "</details>".Length;
        if (end < text.Length && text[end] is '\n' or '\r')
        {
            end += text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? 2 : 1;
        }

        return Replace(text, open, end - open, inner, open, inner.Length);
    }

    public static EditResult NormalizeListMarkers(string text)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var fences = FenceFold.Ranges(text);
        var rebuilt = new System.Text.StringBuilder(text.Length);
        for (var line = 0; line < map.LineCount; line++)
        {
            if (line > 0)
            {
                rebuilt.Append('\n');
            }

            var raw = map.LineText(line);
            if (FenceFold.At(fences, line) is { } fence && line > fence.StartLine && line < fence.EndLine)
            {
                rebuilt.Append(raw);
                continue;
            }

            rebuilt.Append(NormalizeListLine(raw));
        }

        return new EditResult(rebuilt.ToString(), 0, 0);
    }

    public static EditResult? ToSetextHeading(string text, int start)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var range = TocExtractor.RangeAt(TocExtractor.Extract(text), line);
        if (range is not null)
        {
            line = range.Value.StartLine;
        }

        var raw = map.LineText(line);
        var level = HeadingLevel(raw);
        if (level is not 1 and not 2)
        {
            return null;
        }

        var next = line + 1;
        if (next < map.LineCount && IsSetextUnderline(map.LineText(next)))
        {
            return null;
        }

        var title = StripHeading(raw).Trim();
        if (title.Length == 0)
        {
            return null;
        }

        var underline = new string(level == 1 ? '=' : '-', Math.Max(3, title.Length));
        var lines = Lines(map);
        lines[line] = title;
        lines.Insert(line + 1, underline);
        return RebuildLines(lines, text, map.OffsetOfLine(line), title.Length);
    }

    public static EditResult? FromSetextHeading(string text, int start)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var range = TocExtractor.RangeAt(TocExtractor.Extract(text), line);
        if (range is not null)
        {
            line = range.Value.StartLine;
        }

        var raw = map.LineText(line);
        if (HeadingLevel(raw) is >= 1 and <= 6)
        {
            return null;
        }

        var next = line + 1;
        if (next >= map.LineCount || !IsSetextUnderline(map.LineText(next)))
        {
            return null;
        }

        var title = raw.Trim();
        if (title.Length == 0)
        {
            return null;
        }

        var underline = map.LineText(next).Trim();
        var level = underline[0] == '=' ? 1 : 2;
        var atx = new string('#', level) + " " + title;
        var lines = Lines(map);
        lines[line] = atx;
        lines.RemoveAt(next);
        return RebuildLines(lines, text, map.OffsetOfLine(line), atx.Length);
    }

    private static string EnsureTrailingNewline(string block)
        => block.Length == 0 || block.EndsWith('\n') ? block : block + "\n";

    public static EditResult DuplicateLines(string text, int start, int length)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        if (text.Length == 0)
        {
            return new EditResult(text, start, length);
        }

        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var newline = PreferredNewline(text);
        var block = string.Join(newline, Lines(map).Skip(first).Take(last - first + 1));
        var insertAt = map.OffsetOfLine(last + 1);
        string insertion;
        if (insertAt >= text.Length && text.Length > 0 && text[^1] is not ('\r' or '\n'))
        {
            insertion = newline + block;
        }
        else
        {
            insertion = block + newline;
        }

        var rebuilt = text[..insertAt] + insertion + text[insertAt..];
        return new EditResult(rebuilt, start + insertion.Length, length);
    }

    private static List<string> Lines(LineMap map)
    {
        var lines = new List<string>(map.LineCount);
        for (var i = 0; i < map.LineCount; i++)
        {
            lines.Add(map.LineText(i));
        }

        return lines;
    }

    private static (int First, int Last) SelectedLines(LineMap map, int start, int length)
    {
        (start, length) = Clamp(map.Text, start, length);
        return (map.LineOfOffset(start), map.LineOfOffset(length == 0 ? start : start + length - 1));
    }

    private static EditResult RebuildLines(IReadOnlyList<string> lines, string source, int start, int length)
    {
        var newline = PreferredNewline(source);
        var rebuilt = string.Join(newline, lines);
        if (source.EndsWith('\n') || source.EndsWith('\r'))
        {
            if (!rebuilt.EndsWith(newline, StringComparison.Ordinal))
            {
                rebuilt += newline;
            }
        }

        (start, length) = Clamp(rebuilt, start, length);
        return new EditResult(rebuilt, start, length);
    }

    private static string PreferredNewline(string text)
    {
        var index = text.AsSpan().IndexOfAny('\r', '\n');
        return index < 0 || text[index] == '\n' ? "\n" :
            index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";
    }
}

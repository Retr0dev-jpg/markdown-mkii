namespace MarkdownMkII.Core.Text;

public static class ListFold
{
    public static IReadOnlyList<FenceRange> Ranges(string markdown)
    {
        markdown ??= string.Empty;
        var map = new LineMap(markdown);
        var codeLines = MarkdownFence.CodeLines(map);
        var ranges = new List<FenceRange>();
        for (var line = 0; line < map.LineCount; line++)
        {
            if (codeLines[line])
            {
                continue;
            }

            if (!TryListItem(map.LineText(line), out var indent))
            {
                continue;
            }

            var end = line;
            for (var i = line + 1; i < map.LineCount; i++)
            {
                var raw = map.LineText(i);
                if (raw.Trim().Length == 0)
                {
                    end = i;
                    continue;
                }

                if (LeadingWhitespace(raw) > indent)
                {
                    end = i;
                    continue;
                }

                break;
            }

            if (end > line)
            {
                ranges.Add(new FenceRange(line, end));
            }
        }

        return ranges;
    }

    public static FenceRange? At(IReadOnlyList<FenceRange> ranges, int line)
        => FenceFold.At(ranges, line);

    public static bool Toggle(ISet<int> foldedStarts, string markdown, int line)
    {
        var range = At(Ranges(markdown), line);
        if (range is null || range.Value.EndLine <= range.Value.StartLine)
        {
            return false;
        }

        if (!foldedStarts.Add(range.Value.StartLine))
        {
            foldedStarts.Remove(range.Value.StartLine);
        }

        return true;
    }

    public static bool IsLineHidden(string markdown, IReadOnlyCollection<int>? foldedStarts, int line)
    {
        if (foldedStarts is null || foldedStarts.Count == 0)
        {
            return false;
        }

        foreach (var range in Ranges(markdown))
        {
            if (!foldedStarts.Contains(range.StartLine))
            {
                continue;
            }

            if (line > range.StartLine && line <= range.EndLine)
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<(int Start, int Length)> HiddenRanges(string text, IReadOnlyCollection<int>? foldedStarts)
    {
        if (string.IsNullOrEmpty(text) || foldedStarts is null || foldedStarts.Count == 0)
        {
            return [];
        }

        var map = new LineMap(text);
        var ranges = new List<(int Start, int Length)>();
        foreach (var list in Ranges(text))
        {
            if (!foldedStarts.Contains(list.StartLine) || list.EndLine <= list.StartLine)
            {
                continue;
            }

            var from = map.OffsetOfLine(list.StartLine + 1);
            var to = map.OffsetOfLine(list.EndLine + 1);
            var length = to - from;
            if (length > 0)
            {
                ranges.Add((from, length));
            }
        }

        return ranges;
    }

    public static HashSet<int> Starts(string markdown)
        => Ranges(markdown).Select(range => range.StartLine).ToHashSet();

    private static bool TryListItem(string line, out int indent)
    {
        indent = LeadingWhitespace(line);
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
            trimmed.StartsWith("* ", StringComparison.Ordinal) ||
            trimmed.StartsWith("+ ", StringComparison.Ordinal))
        {
            return true;
        }

        var i = 0;
        while (i < trimmed.Length && char.IsDigit(trimmed[i]))
        {
            i++;
        }

        return i > 0 && i < trimmed.Length && trimmed[i] == '.' &&
               i + 1 < trimmed.Length && trimmed[i + 1] == ' ';
    }

    private static int LeadingWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        return i;
    }
}

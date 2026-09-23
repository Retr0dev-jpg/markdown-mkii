namespace MarkdownMkII.Core.Text;

public readonly record struct FenceRange(int StartLine, int EndLine);

public static class FenceFold
{
    public static IReadOnlyList<FenceRange> Ranges(string markdown)
    {
        markdown ??= string.Empty;
        var map = new LineMap(markdown);
        var ranges = new List<FenceRange>();
        var state = new MarkdownFence();
        var open = -1;
        for (var line = 0; line < map.LineCount; line++)
        {
            if (!state.Advance(map.LineText(line)))
            {
                continue;
            }

            if (open < 0)
            {
                open = line;
                continue;
            }

            ranges.Add(new FenceRange(open, line));
            open = -1;
        }

        return ranges;
    }

    public static FenceRange? At(IReadOnlyList<FenceRange> ranges, int line)
    {
        foreach (var range in ranges)
        {
            if (line >= range.StartLine && line <= range.EndLine)
            {
                return range;
            }
        }

        return null;
    }

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
        foreach (var fence in Ranges(text))
        {
            if (!foldedStarts.Contains(fence.StartLine) || fence.EndLine <= fence.StartLine)
            {
                continue;
            }

            var from = map.OffsetOfLine(fence.StartLine + 1);
            var to = map.OffsetOfLine(fence.EndLine + 1);
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
}

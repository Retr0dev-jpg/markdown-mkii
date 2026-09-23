namespace MarkdownMkII.Core.Text;

public static class HeadingFold
{
    public static bool HasBody(OutlineNode node)
        => node.SourceEndLine > node.SourceLine;

    public static IReadOnlyList<OutlineNode> Visible(
        IReadOnlyList<OutlineNode> outline,
        IReadOnlyCollection<int>? foldedStarts)
    {
        if (outline is null || outline.Count == 0)
        {
            return [];
        }

        if (foldedStarts is null || foldedStarts.Count == 0)
        {
            return outline;
        }

        var hidden = new HashSet<int>();
        foreach (var node in outline)
        {
            if (!foldedStarts.Contains(node.SourceLine))
            {
                continue;
            }

            for (var line = node.SourceLine + 1; line <= node.SourceEndLine; line++)
            {
                hidden.Add(line);
            }
        }

        return outline.Where(node => !hidden.Contains(node.SourceLine)).ToList();
    }

    public static OutlineNode? NodeAt(IReadOnlyList<OutlineNode> outline, int line)
    {
        if (outline is null || outline.Count == 0)
        {
            return null;
        }

        OutlineNode? exact = null;
        OutlineNode? enclosing = null;
        foreach (var node in outline)
        {
            if (node.SourceLine == line)
            {
                exact = node;
                break;
            }

            if (line < node.SourceLine || line > node.SourceEndLine)
            {
                continue;
            }

            if (enclosing is null || node.Level >= enclosing.Level)
            {
                enclosing = node;
            }
        }

        return exact ?? enclosing;
    }

    public static bool Toggle(ISet<int> foldedStarts, IReadOnlyList<OutlineNode> outline, int line)
    {
        var node = NodeAt(outline, line);
        if (node is null || !HasBody(node))
        {
            return false;
        }

        if (!foldedStarts.Add(node.SourceLine))
        {
            foldedStarts.Remove(node.SourceLine);
        }

        return true;
    }

    public static void FoldAll(ISet<int> foldedStarts, IReadOnlyList<OutlineNode> outline, string? text = null)
    {
        foreach (var node in outline)
        {
            if (HasBody(node))
            {
                foldedStarts.Add(node.SourceLine);
            }
        }

        if (text is not null)
        {
            foreach (var start in FenceFold.Starts(text))
            {
                foldedStarts.Add(start);
            }

            foreach (var start in ListFold.Starts(text))
            {
                foldedStarts.Add(start);
            }
        }
    }

    public static void UnfoldAll(ISet<int> foldedStarts) => foldedStarts.Clear();

    public static bool IsLineHidden(
        IReadOnlyList<OutlineNode> outline,
        IReadOnlyCollection<int>? foldedStarts,
        int line,
        string? text = null)
    {
        if (outline is not null && foldedStarts is { Count: > 0 })
        {
            foreach (var start in foldedStarts)
            {
                var node = outline.FirstOrDefault(item => item.SourceLine == start);
                if (node is not null && line > node.SourceLine && line <= node.SourceEndLine)
                {
                    return true;
                }
            }
        }

        return text is not null &&
               (FenceFold.IsLineHidden(text, foldedStarts, line) ||
                ListFold.IsLineHidden(text, foldedStarts, line));
    }

    public static IReadOnlyList<(int Start, int Length)> HiddenRanges(
        string text,
        IReadOnlyList<OutlineNode> outline,
        IReadOnlyCollection<int>? foldedStarts)
    {
        if (string.IsNullOrEmpty(text) || foldedStarts is null || foldedStarts.Count == 0)
        {
            return [];
        }

        var map = new LineMap(text);
        var ranges = new List<(int Start, int Length)>();
        if (outline is not null && foldedStarts is { Count: > 0 })
        {
            foreach (var start in foldedStarts)
            {
                var node = outline.FirstOrDefault(item => item.SourceLine == start);
                if (node is null || !HasBody(node))
                {
                    continue;
                }

                var from = map.OffsetOfLine(node.SourceLine + 1);
                var to = map.OffsetOfLine(node.SourceEndLine + 1);
                var length = to - from;
                if (length > 0)
                {
                    ranges.Add((from, length));
                }
            }
        }

        ranges.AddRange(FenceFold.HiddenRanges(text, foldedStarts));
        ranges.AddRange(ListFold.HiddenRanges(text, foldedStarts));
        return ranges;
    }

    public static void Prune(ISet<int> foldedStarts, IReadOnlyList<OutlineNode> outline, string? text = null)
    {
        var valid = new HashSet<int>();
        if (outline is not null)
        {
            foreach (var node in outline)
            {
                valid.Add(node.SourceLine);
            }
        }

        if (text is not null)
        {
            foreach (var start in FenceFold.Starts(text))
            {
                valid.Add(start);
            }

            foreach (var start in ListFold.Starts(text))
            {
                valid.Add(start);
            }
        }

        foreach (var line in foldedStarts.ToList())
        {
            if (!valid.Contains(line))
            {
                foldedStarts.Remove(line);
            }
        }
    }
}

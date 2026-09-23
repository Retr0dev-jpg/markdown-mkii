namespace MarkdownMkII.Core.Text;

public sealed record HeadingRailMark(
    int Line,
    int Level,
    string Title,
    double Top,
    double Height,
    bool Current);

public static class HeadingRail
{
    public static IReadOnlyList<HeadingRailMark> Plan(
        IReadOnlyList<OutlineNode> outline,
        int lineCount,
        double height,
        int currentLine = 0,
        int maxMarks = 24)
    {
        if (outline is null || outline.Count == 0 || lineCount <= 0 || height <= 0)
        {
            return [];
        }

        maxMarks = Math.Clamp(maxMarks, 4, 80);
        var span = Math.Max(1, lineCount);
        var marks = new List<HeadingRailMark>(outline.Count);
        foreach (var node in outline)
        {
            var lines = Math.Max(1, node.SourceEndLine - node.SourceLine + 1);
            var top = node.SourceLine / (double)span * height;
            var markHeight = Math.Max(3, lines / (double)span * height);
            var current = currentLine >= node.SourceLine && currentLine <= node.SourceEndLine;
            marks.Add(new HeadingRailMark(node.SourceLine, node.Level, node.Title, top, markHeight, current));
        }

        if (marks.Count <= maxMarks)
        {
            return marks;
        }

        var filtered = marks.Where(mark => mark.Level <= 2).ToList();
        return (filtered.Count > 0 ? filtered : marks).Take(maxMarks).ToList();
    }
}

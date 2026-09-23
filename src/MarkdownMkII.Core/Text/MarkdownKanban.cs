namespace MarkdownMkII.Core.Text;

public sealed record KanbanCard(string Title, bool Done, int Line, string Column);

public sealed record KanbanColumn(string Name, IReadOnlyList<KanbanCard> Cards);

public static class MarkdownKanban
{
    public static IReadOnlyList<KanbanColumn> Extract(string markdown)
    {
        markdown ??= string.Empty;
        FrontMatter.TrySplit(markdown, out var fields, out _);
        var declared = FrontMatter.List(fields, "kanban", "kanban-columns");
        var map = new LineMap(markdown);
        var outline = TocExtractor.Extract(markdown);
        var codeLines = MarkdownFence.CodeLines(map);
        var buckets = new Dictionary<string, List<KanbanCard>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();

        for (var line = 0; line < map.LineCount; line++)
        {
            if (codeLines[line] || !TryTask(map.LineText(line), out var done, out var title))
            {
                continue;
            }

            var column = ColumnFor(outline, line, done, declared);
            if (!buckets.TryGetValue(column, out var cards))
            {
                cards = [];
                buckets[column] = cards;
                order.Add(column);
            }

            cards.Add(new KanbanCard(title, done, line, column));
        }

        if (declared.Count > 0)
        {
            var merged = new List<string>();
            foreach (var name in declared)
            {
                if (!buckets.ContainsKey(name))
                {
                    buckets[name] = [];
                }

                if (!merged.Exists(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
                {
                    merged.Add(name);
                }
            }

            foreach (var name in order)
            {
                if (!merged.Exists(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
                {
                    merged.Add(name);
                }
            }

            order = merged;
        }

        if (order.Count == 0)
        {
            return [];
        }

        return order.Select(name => new KanbanColumn(name, buckets[name])).ToList();
    }

    public static EditResult Toggle(string text, int line)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        if (map.LineCount == 0)
        {
            return new EditResult(text, 0, 0);
        }

        var start = map.OffsetOfLine(Math.Clamp(line, 0, map.LineCount - 1));
        return MarkdownEditing.ToggleTaskAtCaret(text, start) ?? new EditResult(text, start, 0);
    }

    public static EditResult Move(string text, int line, int columnDelta)
    {
        text ??= string.Empty;
        var columns = Extract(text);
        if (columns.Count == 0)
        {
            return new EditResult(text, 0, 0);
        }

        KanbanCard? card = null;
        var columnIndex = -1;
        for (var i = 0; i < columns.Count; i++)
        {
            var match = columns[i].Cards.FirstOrDefault(item => item.Line == line);
            if (match is null)
            {
                continue;
            }

            card = match;
            columnIndex = i;
            break;
        }

        if (card is null || columnIndex < 0)
        {
            return new EditResult(text, 0, 0);
        }

        var targetIndex = columnIndex + columnDelta;
        if (targetIndex < 0 || targetIndex >= columns.Count)
        {
            return new EditResult(text, 0, 0);
        }

        if (TocExtractor.Extract(text).Count == 0)
        {
            FrontMatter.TrySplit(text, out var fields, out _);
            var declared = FrontMatter.List(fields, "kanban", "kanban-columns");
            var doneColumn = declared.Count >= 2 ? declared[^1] : DoneName;
            return card.Done == string.Equals(columns[targetIndex].Name, doneColumn, StringComparison.OrdinalIgnoreCase)
                ? new EditResult(text, 0, 0)
                : Toggle(text, line);
        }

        if (string.Equals(columns[columnIndex].Name, columns[targetIndex].Name, StringComparison.OrdinalIgnoreCase))
        {
            return new EditResult(text, 0, 0);
        }

        var outline = TocExtractor.Extract(text);
        var heading = outline.FirstOrDefault(node =>
            string.Equals(node.Title, columns[targetIndex].Name, StringComparison.OrdinalIgnoreCase));
        if (heading is null)
        {
            return Toggle(text, line);
        }

        var map = new LineMap(text);
        var start = map.OffsetOfLine(line);
        var end = line + 1 < map.LineCount ? map.OffsetOfLine(line + 1) : text.Length;
        var extracted = text[start..end];
        if (!extracted.EndsWith('\n'))
        {
            extracted += "\n";
        }

        var remaining = text[..start] + text[end..];
        var insertLine = heading.SourceLine + 1;
        if (line < insertLine)
        {
            insertLine--;
        }

        var remainingMap = new LineMap(remaining);
        var insert = remainingMap.OffsetOfLine(Math.Clamp(insertLine, 0, remainingMap.LineCount));
        var next = remaining[..insert] + extracted + remaining[insert..];
        return new EditResult(next, insert, extracted.TrimEnd('\n').Length);
    }

    private static string ColumnFor(
        IReadOnlyList<OutlineNode> outline,
        int line,
        bool done,
        IReadOnlyList<string> declared)
    {
        var range = TocExtractor.RangeAt(outline, line);
        if (range is not null)
        {
            var heading = outline.FirstOrDefault(node => node.SourceLine == range.Value.StartLine);
            if (heading is not null && heading.Title.Length > 0)
            {
                return heading.Title;
            }
        }

        if (declared.Count >= 2)
        {
            return done ? declared[^1] : declared[0];
        }

        return done ? DoneName : TodoName;
    }

    private static bool TryTask(string line, out bool done, out string title)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("- [X] ", StringComparison.OrdinalIgnoreCase))
        {
            done = true;
            title = trimmed[6..].Trim();
            return title.Length > 0;
        }

        if (trimmed.StartsWith("- [ ] ", StringComparison.Ordinal))
        {
            done = false;
            title = trimmed[6..].Trim();
            return title.Length > 0;
        }

        done = false;
        title = string.Empty;
        return false;
    }

    public const string TodoName = "Todo";

    public const string DoneName = "Done";
}

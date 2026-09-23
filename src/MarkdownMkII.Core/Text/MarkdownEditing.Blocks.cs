namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    public static EditResult? SortListItems(string text, int start, int length, bool descending = false)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var lines = Lines(map);
        if (length == 0)
        {
            while (first > 0 && TryListPrefix(lines[first - 1], out _, out _, out _, out _))
            {
                first--;
            }

            while (last + 1 < lines.Count && TryListPrefix(lines[last + 1], out _, out _, out _, out _))
            {
                last++;
            }
        }

        var items = new List<(int Index, string Line, string Key)>();
        for (var i = first; i <= last && i < lines.Count; i++)
        {
            if (!TryListPrefix(lines[i], out _, out _, out var rest, out _))
            {
                continue;
            }

            items.Add((i, lines[i], rest.Trim()));
        }

        if (items.Count < 2)
        {
            return null;
        }

        var sorted = descending
            ? items.OrderByDescending(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList()
            : items.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            lines[items[i].Index] = sorted[i].Line;
        }

        return RebuildLines(lines, text, map.OffsetOfLine(items[0].Index), 0);
    }

    public static EditResult? ConvertList(string text, int start, int length, bool ordered)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var lines = Lines(map);
        if (length == 0)
        {
            while (first > 0 && TryListPrefix(lines[first - 1], out _, out _, out _, out _))
            {
                first--;
            }

            while (last + 1 < lines.Count && TryListPrefix(lines[last + 1], out _, out _, out _, out _))
            {
                last++;
            }
        }

        var converted = false;
        var number = 1;
        for (var i = first; i <= last && i < lines.Count; i++)
        {
            if (!TryListPrefix(lines[i], out var indent, out var marker, out var rest, out _))
            {
                continue;
            }

            var checkbox = string.Empty;
            if (marker.Contains('[', StringComparison.Ordinal))
            {
                var boxAt = marker.IndexOf('[');
                checkbox = marker[boxAt..];
            }
            else if (rest.StartsWith("[ ] ", StringComparison.Ordinal) ||
                     rest.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
            {
                checkbox = rest[..4];
                rest = rest[4..];
            }

            var nextMarker = ordered ? $"{number}. " : "- ";
            if (ordered)
            {
                number++;
            }

            lines[i] = indent + nextMarker + checkbox + rest;
            converted = true;
        }

        if (!converted)
        {
            return null;
        }

        return RebuildLines(lines, text, map.OffsetOfLine(first), 0);
    }

    public static EditResult? CycleBulletMarker(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var lines = Lines(map);
        var changed = false;
        for (var i = first; i <= last && i < lines.Count; i++)
        {
            if (!TryListPrefix(lines[i], out var indent, out var marker, out var rest, out var ordered) ||
                ordered is not null ||
                marker.Length == 0)
            {
                continue;
            }

            var next = marker[0] switch
            {
                '-' => '*',
                '*' => '+',
                '+' => '-',
                _ => '\0'
            };
            if (next == '\0')
            {
                continue;
            }

            lines[i] = indent + next + marker[1..] + rest;
            changed = true;
        }

        if (!changed)
        {
            return null;
        }

        return RebuildLines(lines, text, map.OffsetOfLine(first), 0);
    }

    public static EditResult? IndentList(string text, int start, int length)
        => ShiftListIndent(text, start, length, indent: true);

    public static EditResult? OutdentList(string text, int start, int length)
        => ShiftListIndent(text, start, length, indent: false);

    private static EditResult? ShiftListIndent(string text, int start, int length, bool indent)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var lines = Lines(map);
        if (length == 0)
        {
            while (first > 0 && TryListPrefix(lines[first - 1], out _, out _, out _, out _))
            {
                first--;
            }

            while (last + 1 < lines.Count && TryListPrefix(lines[last + 1], out _, out _, out _, out _))
            {
                last++;
            }
        }

        var changed = false;
        for (var i = first; i <= last && i < lines.Count; i++)
        {
            if (!TryListPrefix(lines[i], out var pad, out _, out _, out _))
            {
                continue;
            }

            if (indent)
            {
                lines[i] = "  " + lines[i];
                changed = true;
                continue;
            }

            if (pad.StartsWith('\t'))
            {
                lines[i] = lines[i][1..];
                changed = true;
            }
            else if (pad.StartsWith("  ", StringComparison.Ordinal))
            {
                lines[i] = lines[i][2..];
                changed = true;
            }
            else if (pad.StartsWith(' '))
            {
                lines[i] = lines[i][1..];
                changed = true;
            }
        }

        if (!changed)
        {
            return null;
        }

        return RebuildLines(lines, text, map.OffsetOfLine(first), 0);
    }

    public static EditResult? QuoteToCallout(string text, int start, int length, string kind = "info")
    {
        text ??= string.Empty;
        kind = string.IsNullOrWhiteSpace(kind) ? "info" : kind.Trim();
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var lines = Lines(map);
        if (length == 0)
        {
            while (first > 0 && lines[first - 1].TrimStart().StartsWith('>'))
            {
                first--;
            }

            while (last + 1 < lines.Count && lines[last + 1].TrimStart().StartsWith('>'))
            {
                last++;
            }
        }

        var body = new List<string>();
        for (var i = first; i <= last && i < lines.Count; i++)
        {
            var raw = lines[i];
            var trimmed = raw.TrimStart();
            if (trimmed.StartsWith('>'))
            {
                var rest = trimmed[1..];
                if (rest.StartsWith(' '))
                {
                    rest = rest[1..];
                }

                body.Add(rest);
                continue;
            }

            if (trimmed.Length == 0)
            {
                body.Add(string.Empty);
                continue;
            }

            return null;
        }

        if (body.Count == 0 || body.All(static line => line.Length == 0))
        {
            return null;
        }

        var callout = new List<string> { ":::" + kind };
        callout.AddRange(body);
        callout.Add(":::");
        var rebuilt = lines.Take(first).Concat(callout).Concat(lines.Skip(last + 1)).ToList();
        return RebuildLines(rebuilt, text, map.OffsetOfLine(first), 0);
    }

    public static EditResult? CalloutToQuote(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var lines = Lines(map);
        if (lines.Count == 0)
        {
            return null;
        }

        var caretLine = map.LineOfOffset(Math.Clamp(start, 0, Math.Max(0, text.Length)));
        var open = -1;
        for (var i = caretLine; i >= 0; i--)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith(":::", StringComparison.Ordinal) && trimmed.Length > 3)
            {
                open = i;
                break;
            }

            if (i != caretLine && trimmed.Equals(":::", StringComparison.Ordinal))
            {
                return null;
            }
        }

        if (open < 0)
        {
            return null;
        }

        var close = -1;
        for (var i = open + 1; i < lines.Count; i++)
        {
            if (lines[i].Trim().Equals(":::", StringComparison.Ordinal))
            {
                close = i;
                break;
            }
        }

        if (close < 0)
        {
            return null;
        }

        var quoted = new List<string>();
        for (var i = open + 1; i < close; i++)
        {
            quoted.Add(lines[i].Length == 0 ? ">" : "> " + lines[i]);
        }

        if (quoted.Count == 0 || quoted.All(static line => line is ">" or "> "))
        {
            return null;
        }

        var rebuilt = lines.Take(open).Concat(quoted).Concat(lines.Skip(close + 1)).ToList();
        return RebuildLines(rebuilt, text, map.OffsetOfLine(open), 0);
    }

    public static EditResult? UnwrapCallout(string text, int start, int length)
        => CalloutToQuote(text, start, length);

    public static EditResult? CycleCalloutKind(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var lines = Lines(map);
        if (lines.Count == 0)
        {
            return null;
        }

        var caretLine = map.LineOfOffset(Math.Clamp(start, 0, Math.Max(0, text.Length)));
        var open = -1;
        for (var i = caretLine; i >= 0; i--)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith(":::", StringComparison.Ordinal) && trimmed.Length > 3)
            {
                open = i;
                break;
            }

            if (i != caretLine && trimmed.Equals(":::", StringComparison.Ordinal))
            {
                return null;
            }
        }

        if (open < 0)
        {
            return null;
        }

        var raw = lines[open];
        var lead = raw.Length - raw.TrimStart().Length;
        var kind = raw.Trim()[3..].Trim();
        if (kind.Length == 0)
        {
            return null;
        }

        string[] kinds = ["info", "warning", "tip", "danger", "note"];
        var index = Array.FindIndex(kinds, item => string.Equals(item, kind, StringComparison.OrdinalIgnoreCase));
        var next = kinds[(index + 1) % kinds.Length];
        if (string.Equals(next, kind, StringComparison.OrdinalIgnoreCase) && index >= 0)
        {
            next = kinds[(index + 1) % kinds.Length];
        }

        lines[open] = raw[..lead] + ":::" + next;
        return RebuildLines(lines, text, map.OffsetOfLine(open), 0);
    }

    public static EditResult InsertCallout(string text, int start, int length, string kind = "info")
    {
        text ??= string.Empty;
        kind = string.IsNullOrWhiteSpace(kind) ? "info" : kind.Trim();
        (start, length) = Clamp(text, start, length);
        var body = length > 0 ? text.Substring(start, length).Trim('\r', '\n') : string.Empty;
        var block = ":::" + kind + "\n" + (body.Length == 0 ? string.Empty : body + "\n") + ":::";
        var inner = start + 3 + kind.Length + 1;
        return Replace(text, start, length, block, inner, body.Length);
    }

    public static EditResult? LinesToTaskList(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var lines = Lines(map);
        var changed = false;
        for (var i = first; i <= last && i < lines.Count; i++)
        {
            var next = ToTaskLine(lines[i]);
            if (!string.Equals(next, lines[i], StringComparison.Ordinal))
            {
                changed = true;
            }

            lines[i] = next;
        }

        if (!changed)
        {
            return null;
        }

        return RebuildLines(lines, text, map.OffsetOfLine(first), 0);
    }

    private static string ToTaskLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        if (TryListPrefix(line, out var indent, out var marker, out var rest, out _))
        {
            if (marker.StartsWith("- [", StringComparison.Ordinal) ||
                marker.StartsWith("* [", StringComparison.Ordinal))
            {
                return line;
            }

            var restTrim = rest.TrimStart();
            if (restTrim.StartsWith("[ ] ", StringComparison.Ordinal) ||
                restTrim.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
            {
                return indent + "- " + restTrim;
            }

            return indent + "- [ ] " + rest;
        }

        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        return line[..i] + "- [ ] " + line[i..];
    }

    public static EditResult? TaskListToLines(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var lines = Lines(map);
        var changed = false;
        for (var i = first; i <= last && i < lines.Count; i++)
        {
            var next = FromTaskLine(lines[i]);
            if (!string.Equals(next, lines[i], StringComparison.Ordinal))
            {
                changed = true;
            }

            lines[i] = next;
        }

        if (!changed)
        {
            return null;
        }

        return RebuildLines(lines, text, map.OffsetOfLine(first), 0);
    }

    private static string FromTaskLine(string line)
    {
        if (!TryListPrefix(line, out var indent, out var marker, out var rest, out _))
        {
            return line;
        }

        if (marker.StartsWith("- [", StringComparison.Ordinal) ||
            marker.StartsWith("* [", StringComparison.Ordinal))
        {
            return indent + rest;
        }

        var restTrim = rest.TrimStart();
        if (restTrim.StartsWith("[ ] ", StringComparison.Ordinal) ||
            restTrim.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
        {
            return indent + restTrim[4..];
        }

        return line;
    }
}

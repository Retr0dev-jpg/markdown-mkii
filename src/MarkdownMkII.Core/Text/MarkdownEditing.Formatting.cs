namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    public static EditResult FormatDocument(string text)
    {
        text ??= string.Empty;
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var fence = new MarkdownFence();
        var formatted = new List<string>(lines.Length);
        var blank = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index];
            var inside = fence.IsOpen;
            if (fence.Advance(raw) || inside)
            {
                formatted.Add(raw);
                blank = 0;
                continue;
            }

            var line = raw.TrimEnd();
            // Two trailing spaces before another line of the same paragraph are a hard line break.
            if (line.Length > 0 && raw.EndsWith("  ", StringComparison.Ordinal) &&
                index + 1 < lines.Length && lines[index + 1].Trim().Length > 0)
            {
                line += "  ";
            }
            blank = line.Length == 0 ? blank + 1 : 0;
            if (blank <= 2)
            {
                formatted.Add(line);
            }
        }

        var result = string.Join('\n', formatted);
        if (result.Length > 0 && !result.EndsWith('\n'))
        {
            result += "\n";
        }

        return new EditResult(RenumberOrderedLists(result), 0, 0);
    }

    public static string StripTrailingWhitespace(string markdown)
    {
        markdown ??= string.Empty;
        var map = new LineMap(markdown);
        var fence = new MarkdownFence();
        var rebuilt = new System.Text.StringBuilder(markdown.Length);
        for (var line = 0; line < map.LineCount; line++)
        {
            var raw = map.LineText(line);
            var inside = fence.IsOpen;
            var boundary = fence.Advance(raw);
            rebuilt.Append(inside || boundary ? raw : raw.TrimEnd());
            var span = map.LineSpan(line);
            rebuilt.Append(markdown, span.Start + raw.Length, span.Length - raw.Length);
        }

        return rebuilt.ToString();
    }

    public static EditResult NumberHeadings(string text)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var fences = FenceFold.Ranges(text);
        var protectedLines = ProtectedLines(text, map);
        var counters = new int[7];
        var rebuilt = new System.Text.StringBuilder(text.Length);
        for (var line = 0; line < map.LineCount; line++)
        {
            if (line > 0)
            {
                rebuilt.Append('\n');
            }

            var raw = map.LineText(line);
            if (protectedLines[line] || FenceFold.At(fences, line) is { } fence && line > fence.StartLine && line < fence.EndLine)
            {
                rebuilt.Append(raw);
                continue;
            }

            var level = HeadingLevel(raw);
            if (level is < 1 or > 6)
            {
                rebuilt.Append(raw);
                continue;
            }

            var hashes = new string('#', level);
            var title = raw.TrimStart();
            title = title[level..].TrimStart();
            title = StripHeadingNumber(title);
            for (var i = level + 1; i < counters.Length; i++)
            {
                counters[i] = 0;
            }

            counters[level]++;
            var parts = new List<string>(level);
            for (var i = 1; i <= level; i++)
            {
                parts.Add(counters[i].ToString());
            }

            rebuilt.Append(hashes).Append(' ').Append(string.Join('.', parts)).Append(' ').Append(title);
        }

        if (text.EndsWith('\n') && rebuilt.Length > 0 && rebuilt[^1] != '\n')
        {
            rebuilt.Append('\n');
        }

        return new EditResult(rebuilt.ToString(), 0, 0);
    }

    private static string StripHeadingNumber(string title)
    {
        title ??= string.Empty;
        var i = 0;
        while (i < title.Length && char.IsDigit(title[i]))
        {
            while (i < title.Length && char.IsDigit(title[i]))
            {
                i++;
            }

            if (i < title.Length && title[i] == '.')
            {
                i++;
                continue;
            }

            if (i < title.Length && title[i] == ' ')
            {
                return title[(i + 1)..].TrimStart();
            }

            break;
        }

        return title;
    }

    /// <summary>
    /// Renumbers each ordered list from its first number, keeping its delimiter. Continuation lines,
    /// indented code and blank lines inside items keep the list open; a margin paragraph after a blank
    /// line, a margin fence or a bullet at the same depth end it.
    /// </summary>
    private static string RenumberOrderedLists(string text)
    {
        var map = new LineMap(text);
        var lines = Lines(map);
        var counters = new Dictionary<int, (int Next, char Delimiter)>();
        var codeLines = MarkdownFence.CodeLines(map);
        var previousBlank = true;
        static bool Indented(string line) => line.Length > 0 && line[0] is ' ' or '\t';
        void DropFrom(int depth)
        {
            foreach (var key in counters.Keys.Where(key => key >= depth).ToList()) counters.Remove(key);
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (codeLines[i])
            {
                if ((i == 0 || !codeLines[i - 1]) && !Indented(line)) counters.Clear();
                previousBlank = false;
                continue;
            }

            if (line.Trim().Length == 0)
            {
                previousBlank = true;
                continue;
            }

            var isItem = TryListPrefix(line, out var indent, out var marker, out var rest, out var number);
            if (!isItem)
            {
                if (!Indented(line) && previousBlank) counters.Clear();
                previousBlank = false;
                continue;
            }

            var depth = indent.Length;
            if (number is null)
            {
                DropFrom(depth);
                previousBlank = false;
                continue;
            }

            DropFrom(depth + 1);
            var delimiter = marker.TrimEnd()[^1];
            if (!counters.TryGetValue(depth, out var state) || state.Delimiter != delimiter)
            {
                state = (number.Value, delimiter);
            }

            lines[i] = indent + state.Next + delimiter + " " + rest;
            counters[depth] = (state.Next + 1, delimiter);
            previousBlank = false;
        }

        return string.Join('\n', lines);
    }

    /// <summary>Lines that text commands must not rewrite: fenced code and the front matter block.</summary>
    private static bool[] ProtectedLines(string text, LineMap map)
    {
        var result = MarkdownFence.CodeLines(map);
        if (FrontMatter.TryLocate(text, out _, out _, out var bodyStart))
        {
            var last = map.LineOfOffset(Math.Max(0, bodyStart - 1));
            for (var i = 0; i <= last && i < result.Length; i++) result[i] = true;
        }

        return result;
    }

    public static EditResult JoinLines(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        if (first == last && last < map.LineCount - 1)
        {
            last++;
        }

        var protectedLines = ProtectedLines(text, map);
        if (Enumerable.Range(first, last - first + 1).Any(line => protectedLines[line]))
        {
            return new EditResult(text, start, length);
        }

        var lines = Lines(map);
        var joined = string.Join(' ', lines.Skip(first).Take(last - first + 1).Select(l => l.Trim()).Where(l => l.Length > 0));
        var rebuilt = lines.Take(first).Append(joined).Concat(lines.Skip(last + 1)).ToList();
        return RebuildLines(rebuilt, text, map.OffsetOfLine(first), joined.Length);
    }

    public static EditResult ReflowParagraph(string text, int start, int length)
        => ReflowParagraph(text, start, length, 80);

    public static EditResult ReflowParagraph(string text, int start, int length, int width)
    {
        text ??= string.Empty;
        width = width <= 0 ? 80 : width;
        (start, length) = Clamp(text, start, length);
        var map = new LineMap(text);
        if (map.LineCount == 0 || text.Length == 0)
        {
            return new EditResult(text, start, length);
        }

        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var protectedLines = ProtectedLines(text, map);
        if (protectedLines[line])
        {
            return new EditResult(text, start, length);
        }

        var first = line;
        while (first > 0 && !protectedLines[first - 1] && map.LineText(first - 1).Trim().Length > 0)
        {
            first--;
        }

        var last = line;
        while (last + 1 < map.LineCount && !protectedLines[last + 1] && map.LineText(last + 1).Trim().Length > 0)
        {
            last++;
        }

        var firstText = map.LineText(first);
        var trimmed = firstText.TrimStart();
        if (trimmed.StartsWith('#') ||
            trimmed.StartsWith("```", StringComparison.Ordinal) ||
            trimmed.StartsWith("~~~", StringComparison.Ordinal) ||
            trimmed.StartsWith('|') ||
            IsListLine(firstText, ordered: false) ||
            IsListLine(firstText, ordered: true))
        {
            return new EditResult(text, start, length);
        }

        var lines = Lines(map);
        var paragraph = string.Join(' ', lines.Skip(first).Take(last - first + 1).Select(item => item.Trim()).Where(item => item.Length > 0));
        if (paragraph.Length == 0)
        {
            return new EditResult(text, start, length);
        }

        var wrapped = WrapWords(paragraph, width);
        var rebuilt = lines.Take(first).Concat(wrapped).Concat(lines.Skip(last + 1)).ToList();
        var selectionLength = wrapped.Sum(item => item.Length) + Math.Max(0, wrapped.Count - 1);
        return RebuildLines(rebuilt, text, map.OffsetOfLine(first), selectionLength);
    }

    private static IReadOnlyList<string> WrapWords(string paragraph, int width)
    {
        var words = paragraph.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length <= width)
            {
                current.Append(' ').Append(word);
                continue;
            }

            lines.Add(current.ToString());
            current.Clear();
            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }

    public static EditResult ChangeCase(string text, int start, int length, bool upper)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        if (length == 0)
        {
            var (wordStart, wordLength) = CurrentWord(text, start);
            start = wordStart;
            length = wordLength;
        }

        if (length == 0)
        {
            return new EditResult(text, start, 0);
        }

        var selected = text.Substring(start, length);
        var updated = CaseOutsideCode(selected, value => upper ? value.ToUpperInvariant() : value.ToLowerInvariant());
        return Replace(text, start, length, updated, start, updated.Length);
    }

    public static EditResult ApplyTypographer(string text)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var builder = new System.Text.StringBuilder(text.Length);
        var protectedLines = ProtectedLines(text, map);
        for (var i = 0; i < map.LineCount; i++)
        {
            var line = map.LineText(i);
            var trimmed = line.Trim();
            var rule = trimmed.Length >= 3 && trimmed.All(c => c is '-' or '=' or '*' or '_' or ' ');
            if (!protectedLines[i] && !rule)
            {
                line = ApplyTypographerLine(line);
            }

            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
        }

        return new EditResult(builder.ToString(), 0, 0);
    }

    private static string ApplyTypographerLine(string line)
    {
        var urls = new List<string>();
        string protectedLine;
        try
        {
            protectedLine = System.Text.RegularExpressions.Regex.Replace(
                line,
                @"(`+).*?\1|https?://\S+|<[^<>\s]+>",
                match =>
                {
                    urls.Add(match.Value);
                    return "\u0001" + (urls.Count - 1) + "\u0001";
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(50));
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            protectedLine = line;
        }

        protectedLine = protectedLine.Replace("---", "—", StringComparison.Ordinal)
            .Replace("...", "…", StringComparison.Ordinal)
            .Replace("--", "–", StringComparison.Ordinal);
        protectedLine = ApplySmartQuotes(protectedLine);
        for (var i = 0; i < urls.Count; i++)
        {
            protectedLine = protectedLine.Replace("\u0001" + i + "\u0001", urls[i], StringComparison.Ordinal);
        }

        return protectedLine;
    }

    private static string ApplySmartQuotes(string line)
    {
        var builder = new System.Text.StringBuilder(line.Length);
        var opening = true;
        foreach (var ch in line)
        {
            if (ch is '"')
            {
                builder.Append(opening ? '“' : '”');
                opening = !opening;
                continue;
            }

            if (ch is '\'')
            {
                var prev = builder.Length == 0 ? ' ' : builder[^1];
                builder.Append(char.IsLetterOrDigit(prev) ? '’' : '‘');
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}

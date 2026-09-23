using MarkdownMkII.Core.Markdown;

namespace MarkdownMkII.Core.Text;

public readonly record struct EditResult(string Text, int SelectionStart, int SelectionLength);

/// <summary>
/// Trasformazioni sul testo Markdown indipendenti dalla UI: wrap, heading, liste, tabelle, fence.
/// </summary>
public static partial class MarkdownEditing
{
    public static EditResult ToggleWrap(string text, int start, int length, string prefix, string suffix)
    {
        text ??= string.Empty;
        prefix ??= string.Empty;
        suffix ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var selected = text.Substring(start, length);

        if (prefix.Length > 0 && prefix == suffix && prefix.All(c => c == prefix[0]))
        {
            return ToggleRepeatedMarker(text, start, length, prefix[0], prefix.Length);
        }

        if (selected.StartsWith(prefix, StringComparison.Ordinal) &&
            selected.EndsWith(suffix, StringComparison.Ordinal) &&
            selected.Length >= prefix.Length + suffix.Length)
        {
            var inner = selected[prefix.Length..^suffix.Length];
            return Replace(text, start, length, inner, start, inner.Length);
        }

        var before = start >= prefix.Length ? text.Substring(start - prefix.Length, prefix.Length) : string.Empty;
        var afterEnd = start + length;
        var after = afterEnd + suffix.Length <= text.Length
            ? text.Substring(afterEnd, suffix.Length)
            : string.Empty;

        if (before == prefix && after == suffix)
        {
            return Replace(text, start - prefix.Length, length + prefix.Length + suffix.Length, selected, start - prefix.Length, selected.Length);
        }

        var wrapped = prefix + selected + suffix;
        return Replace(text, start, length, wrapped, start + prefix.Length, selected.Length);
    }

    /// <summary>
    /// Carries a selection from <paramref name="before"/> into <paramref name="after"/>: it keeps its place
    /// when the change is elsewhere, shifts after it, and collapses to the start of a change it overlaps.
    /// </summary>
    public static EditResult MapSelection(string before, string after, int start, int length)
    {
        before ??= string.Empty;
        after ??= string.Empty;
        var prefix = 0;
        var limit = Math.Min(before.Length, after.Length);
        while (prefix < limit && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < limit - prefix && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;
        var changedEnd = before.Length - suffix;
        var delta = after.Length - before.Length;
        if (start + length <= prefix) return new EditResult(after, start, Math.Min(length, after.Length - start));
        if (start >= changedEnd) return new EditResult(after, Math.Clamp(start + delta, 0, after.Length), Math.Min(length, Math.Max(0, after.Length - start - delta)));
        return new EditResult(after, Math.Min(prefix, after.Length), 0);
    }

    /// <summary>
    /// Toggles <c>*</c>/<c>**</c> or <c>~</c>/<c>~~</c> by the length of the marker run around the text:
    /// a single marker is present on odd runs (1, 3), a double marker on runs of two or more.
    /// </summary>
    private static EditResult ToggleRepeatedMarker(string text, int start, int length, char marker, int size)
    {
        bool Present(int run) => size == 1 ? run % 2 == 1 : run >= size;

        var innerLeft = 0;
        while (innerLeft < length && text[start + innerLeft] == marker) innerLeft++;
        var innerRight = 0;
        while (innerRight < length - innerLeft && text[start + length - 1 - innerRight] == marker) innerRight++;
        if (innerLeft < length && Present(innerLeft) && Present(innerRight))
        {
            var inner = text.Substring(start + size, length - 2 * size);
            return Replace(text, start, length, inner, start, inner.Length);
        }

        var outerLeft = 0;
        while (start - outerLeft - 1 >= 0 && text[start - outerLeft - 1] == marker) outerLeft++;
        var outerRight = 0;
        while (start + length + outerRight < text.Length && text[start + length + outerRight] == marker) outerRight++;
        if (Present(outerLeft) && Present(outerRight))
        {
            var selected = text.Substring(start, length);
            return Replace(text, start - size, length + 2 * size, selected, start - size, length);
        }

        var markers = new string(marker, size);
        return Replace(text, start, length, markers + text.Substring(start, length) + markers, start + size, length);
    }

    private static EditResult ToggleDecoration(string text, int start, int length, string marker)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var end = start + length;
        var originalStart = start;
        var originalLength = length;
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        if (originalLength > 0 && start == end) return new(text, originalStart, originalLength);
        return ToggleWrap(text, start, end - start, marker, marker);
    }

    public static EditResult ToggleBold(string text, int start, int length)
        => ToggleDecoration(text, start, length, "**");

    public static EditResult ToggleItalic(string text, int start, int length)
        => ToggleDecoration(text, start, length, "*");

    public static EditResult ToggleStrikethrough(string text, int start, int length)
        => ToggleDecoration(text, start, length, "~~");

    public static EditResult ToggleInlineCode(string text, int start, int length)
        => ToggleWrap(text, start, length, "`", "`");

    public static EditResult ToggleHighlight(string text, int start, int length)
        => ToggleDecoration(text, start, length, "==");

    public static EditResult ToggleSuperscript(string text, int start, int length)
        => ToggleDecoration(text, start, length, "^");

    public static EditResult ToggleSubscript(string text, int start, int length)
        => ToggleDecoration(text, start, length, "~");

    public static EditResult ToggleInserted(string text, int start, int length)
        => ToggleDecoration(text, start, length, "++");

    public static EditResult ToggleHeading(string text, int start, int length, int level)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        if (AnyCodeLine(map, first, last))
        {
            return new EditResult(text, start, length);
        }

        level = Math.Clamp(level, 0, 6);
        if (level > 0 && Enumerable.Range(first, last - first + 1).All(line => HeadingLevel(map.LineText(line)) == level))
        {
            level = 0;
        }

        var prefix = level == 0 ? string.Empty : new string('#', level) + " ";
        return TransformLines(text, first, last, line => prefix + StripHeading(line), start, length);
    }

    public static EditResult CycleHeading(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var current = HeadingLevel(map.LineText(line));
        var next = current >= 6 ? 0 : current + 1;
        return ToggleHeading(text, start, length, next);
    }

    public static EditResult ShiftHeadingLevel(string text, int start, int length, int delta)
    {
        text ??= string.Empty;
        if (delta == 0 || text.Length == 0)
        {
            return new EditResult(text, start, length);
        }

        var map = new LineMap(text);
        var (firstLine, lastLine) = SelectedLines(map, start, length);
        return TransformLines(text, firstLine, lastLine, lineText =>
        {
            var level = HeadingLevel(lineText);
            if (level is < 1 or > 6)
            {
                return lineText;
            }

            var next = Math.Clamp(level + delta, 1, 6);
            if (next == level)
            {
                return lineText;
            }

            var indent = lineText.Length - lineText.TrimStart().Length;
            return lineText[..indent] + new string('#', next) + " " + StripHeading(lineText).TrimStart();
        }, start, length);
    }

    public static (string? OldTitle, EditResult Result)? RenameHeading(string text, int start, string newTitle)
    {
        text ??= string.Empty;
        newTitle = (newTitle ?? string.Empty).Trim();
        if (newTitle.Length == 0)
        {
            return null;
        }

        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var raw = map.LineText(line);
        var level = HeadingLevel(raw);
        if (level is < 1 or > 6)
        {
            return null;
        }

        var oldTitle = raw.TrimStart()[level..].Trim();
        if (oldTitle.StartsWith(' '))
        {
            oldTitle = oldTitle.Trim();
        }

        if (oldTitle.Length == 0 || string.Equals(oldTitle, newTitle, StringComparison.Ordinal))
        {
            return null;
        }

        var hashes = new string('#', level);
        var replacement = hashes + " " + newTitle;
        var span = map.LineSpan(line);
        var contentLength = map.LineText(line).Length;
        var next = text[..span.Start] + replacement + text[(span.Start + contentLength)..];
        next = WikiLinks.RewriteHeading(next, string.Empty, oldTitle, newTitle);
        return (oldTitle, new EditResult(next, span.Start, replacement.Length));
    }

    /// <summary>ATX heading level; CommonMark allows up to three spaces of indentation.</summary>
    public static int HeadingLevel(string line)
    {
        line ??= string.Empty;
        var indent = 0;
        while (indent < 3 && indent < line.Length && line[indent] == ' ')
        {
            indent++;
        }

        var i = indent;
        while (i < line.Length && line[i] == '#')
        {
            i++;
        }

        var hashes = i - indent;
        if (hashes > 0 && hashes <= 6 && (i == line.Length || line[i] == ' '))
        {
            return hashes;
        }

        return 0;
    }

    public static EditResult ToggleList(string text, int start, int length, bool ordered)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        if (AnyCodeLine(map, first, last))
        {
            return new EditResult(text, start, length);
        }

        var content = Enumerable.Range(first, last - first + 1).Select(map.LineText).Where(line => line.Trim().Length > 0).ToList();
        var allListed = content.Count > 0 && content.All(line => IsListLine(line, ordered));
        var number = 1;
        return TransformLines(text, first, last, line =>
        {
            if (line.Trim().Length == 0) return line;
            var stripped = StripList(line);
            return allListed ? stripped : ordered ? $"{number++}. {stripped}" : "- " + stripped;
        }, start, length);
    }

    public static EditResult ToggleQuote(string text, int start, int length)
        => ToggleLinePrefix(text, start, length, "> ");

    public static EditResult NestQuote(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (firstLine, lastLine) = SelectedLines(map, start, length);
        return TransformLines(text, firstLine, lastLine, static line => "> " + line, start, length);
    }

    public static EditResult UnnestQuote(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (firstLine, lastLine) = SelectedLines(map, start, length);
        return TransformLines(text, firstLine, lastLine, static line =>
        {
            var indentEnd = 0;
            while (indentEnd < line.Length && (line[indentEnd] == ' ' || line[indentEnd] == '\t'))
            {
                indentEnd++;
            }

            var rest = line[indentEnd..];
            if (rest.StartsWith("> ", StringComparison.Ordinal))
            {
                return line[..indentEnd] + rest[2..];
            }

            if (rest.StartsWith('>'))
            {
                return line[..indentEnd] + rest[1..];
            }

            return line;
        }, start, length);
    }

    public static EditResult ToggleTaskItem(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (firstLine, lastLine) = SelectedLines(map, start, length);
        if (AnyCodeLine(map, firstLine, lastLine))
        {
            return new EditResult(text, start, length);
        }

        var allTasks = true;
        for (var line = firstLine; line <= lastLine; line++)
        {
            if (!IsTaskLine(map.LineText(line)))
            {
                allTasks = false;
                break;
            }
        }

        return TransformLines(text, firstLine, lastLine, lineText =>
        {
            if (allTasks)
            {
                return StripList(lineText);
            }

            return "- [ ] " + StripList(lineText);
        }, start, length);
    }

    public static EditResult SetTasksChecked(string text, int start, int length, bool isChecked)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        return TransformLines(text, first, last, line => SetTaskMarker(line, isChecked), start, length);
    }

    private static string SetTaskMarker(string line, bool isChecked)
        => System.Text.RegularExpressions.Regex.Replace(
            line,
            @"^(\s*(?:>\s?)*\s*(?:[-*+]|[0-9]{1,9}[.)])\s+)\[(?: |x|X)\](?=\s|$)",
            match => match.Groups[1].Value + (isChecked ? "[x]" : "[ ]"),
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(50));

    public static EditResult SetTaskChecked(string text, int sourceLine, bool isChecked)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        if (sourceLine < 0 || sourceLine >= map.LineCount)
        {
            return new EditResult(text, 0, 0);
        }

        var line = map.LineText(sourceLine);
        var span = map.LineSpan(sourceLine);
        var updated = SetTaskMarker(line, isChecked);

        if (updated == line)
        {
            return new EditResult(text, span.Start, 0);
        }

        var newText = text[..span.Start] + updated + text[(span.Start + line.Length)..];
        return new EditResult(newText, span.Start, 0);
    }

    public static EditResult? ToggleTaskAtCaret(string text, int caret)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(caret, 0, text.Length));
        var lineText = map.LineText(line);
        var match = System.Text.RegularExpressions.Regex.Match(
            lineText,
            @"^\s*(?:>\s?)*\s*(?:[-*+]|\d+[.)])\s+\[( |x|X)\]",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromMilliseconds(50));
        if (!match.Success)
        {
            return null;
        }

        var isChecked = match.Groups[1].Value is "x" or "X";
        return SetTaskChecked(text, line, !isChecked);
    }

    public static EditResult DeleteLines(string text, int start, int length)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return new EditResult(text, 0, 0);
        }

        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var spanStart = map.OffsetOfLine(first);
        var spanEnd = last + 1 < map.LineCount ? map.OffsetOfLine(last + 1) : text.Length;
        var rebuilt = text[..spanStart] + text[spanEnd..];
        var caret = Math.Min(spanStart, rebuilt.Length);
        return new EditResult(rebuilt, caret, 0);
    }

    public static EditResult DuplicateLine(string text, int start, int length)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return new EditResult(text, 0, 0);
        }

        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var spanStart = map.OffsetOfLine(first);
        var spanEnd = map.OffsetOfLine(last + 1);
        var result = DuplicateLines(text, spanStart, spanEnd - spanStart);
        return result with { SelectionLength = 0 };
    }

    public static (int Start, int Length) LineSelection(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var selStart = map.OffsetOfLine(first);
        var selEnd = last + 1 < map.LineCount ? map.OffsetOfLine(last + 1) : text.Length;
        return (selStart, Math.Max(0, selEnd - selStart));
    }

    public static EditResult InsertMath(string text, int start, int length)
        => ToggleWrap(text, start, length, "$", "$");

    public static EditResult InsertMathBlock(string text, int start, int length)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var body = length > 0 ? text.Substring(start, length).Trim('\r', '\n') : string.Empty;
        return InsertFencedBlock(text, start, length, "$$", "$$", body);
    }

    public static EditResult TitleCase(string text, int start, int length)
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
        var updated = CaseOutsideCode(selected, ToTitleCase);
        return Replace(text, start, length, updated, start, updated.Length);
    }

    public static EditResult SentenceCase(string text, int start, int length)
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
        var updated = CaseOutsideCode(selected, ToSentenceCase);
        return Replace(text, start, length, updated, start, updated.Length);
    }

    /// <summary>
    /// Applies a length-preserving case transform everywhere except inline code, URLs, autolinks and
    /// link destinations, which are masked with inert characters and restored afterwards.
    /// </summary>
    internal static string CaseOutsideCode(string value, Func<string, string> transform)
    {
        System.Text.RegularExpressions.MatchCollection matches;
        try
        {
            matches = System.Text.RegularExpressions.Regex.Matches(value, @"(`+).*?\1|https?://\S+|<[^<>\s]+>|\]\([^)\s]*\)",
                System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(100));
            _ = matches.Count;
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return transform(value);
        }

        var masked = value.ToCharArray();
        foreach (System.Text.RegularExpressions.Match match in matches)
            Array.Fill(masked, '\u0001', match.Index, match.Length);
        var result = transform(new string(masked)).ToCharArray();
        if (result.Length != value.Length)
            return transform(value);
        foreach (System.Text.RegularExpressions.Match match in matches)
            value.CopyTo(match.Index, result, match.Index, match.Length);
        return new string(result);
    }

    private static string ToSentenceCase(string value)
    {
        var chars = value.ToCharArray();
        var capitalize = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetter(chars[i]))
            {
                chars[i] = capitalize ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
                capitalize = false;
            }
            else if (chars[i] is '.' or '!' or '?')
            {
                capitalize = true;
            }
        }

        return new string(chars);
    }

    private static string ToTitleCase(string value)
    {
        var chars = value.ToCharArray();
        var newWord = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsLetter(chars[i]))
            {
                chars[i] = newWord ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
                newWord = false;
            }
            else if (char.IsWhiteSpace(chars[i]) || chars[i] is '-' or '_' or '/')
            {
                newWord = true;
            }
        }

        return new string(chars);
    }

    public static EditResult InsertLink(string text, int start, int length, string url, string? title = null)
    {
        text ??= string.Empty;
        url ??= "https://";
        (start, length) = Clamp(text, start, length);
        var label = length > 0 ? text.Substring(start, length) : SuggestLinkLabel(url);
        var titlePart = string.IsNullOrWhiteSpace(title) ? string.Empty : $" \"{title}\"";
        var inserted = $"[{label}]({url}{titlePart})";
        return Replace(text, start, length, inserted, start + 1, label.Length);
    }

    private static string SuggestLinkLabel(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return "link";
    }

    public static EditResult InsertImage(string text, int start, int length, string path, string? alt = null)
    {
        text ??= string.Empty;
        path ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var label = alt ?? (length > 0 ? text.Substring(start, length) : "image");
        var inserted = $"![{label}]({path})";
        return Replace(text, start, length, inserted, start, inserted.Length);
    }

    public static EditResult InsertText(string text, int start, int length, string insertion)
    {
        text ??= string.Empty;
        insertion ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        return Replace(text, start, length, insertion, start + insertion.Length, 0);
    }

    public static EditResult Unindent(string text, int start, int length, int tabSize)
    {
        text ??= string.Empty;
        tabSize = Math.Clamp(tabSize, 2, 8);
        var map = new LineMap(text);
        var (firstLine, lastLine) = SelectedLines(map, start, length);
        return TransformLines(text, firstLine, lastLine, line => StripIndent(line, tabSize), start, length);
    }

    public static EditResult Indent(string text, int start, int length, int tabSize)
    {
        text ??= string.Empty;
        tabSize = Math.Clamp(tabSize, 2, 8);
        var pad = new string(' ', tabSize);
        var map = new LineMap(text);
        var (firstLine, lastLine) = SelectedLines(map, start, length);
        return TransformLines(text, firstLine, lastLine, line => pad + line, start, length);
    }

    private static string StripIndent(string line, int tabSize)
    {
        if (line.StartsWith('\t'))
        {
            return line[1..];
        }

        var removed = 0;
        while (removed < line.Length && removed < tabSize && line[removed] == ' ')
        {
            removed++;
        }

        return removed == 0 ? line : line[removed..];
    }

    public static EditResult InsertCodeFence(string text, int start, int length, string? language = "csharp")
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var selected = text.Substring(start, length);
        language ??= string.Empty;
        var body = selected.Length == 0 ? string.Empty : selected.Trim('\r', '\n');
        var longest = 0;
        var run = 0;
        foreach (var ch in body)
        {
            run = ch == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        var marker = new string('`', Math.Max(3, longest + 1));
        return InsertFencedBlock(text, start, length, marker + language, marker, body);
    }

    private static EditResult InsertFencedBlock(string text, int start, int length, string opening, string closing, string body)
    {
        var newline = PreferredNewline(text);
        var before = start > 0 && text[start - 1] is not ('\r' or '\n') ? newline : string.Empty;
        var after = start + length < text.Length && text[start + length] is not ('\r' or '\n') ? newline : string.Empty;
        var block = before + opening + newline + body + newline + closing + after;
        return Replace(text, start, length, block, start + before.Length + opening.Length + newline.Length, body.Length);
    }

    public static EditResult InsertTable(string text, int caret, int rows, int columns)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        rows = Math.Clamp(rows, 1, 20);
        columns = Math.Clamp(columns, 1, 12);

        var header = string.Join(" | ", Enumerable.Range(1, columns).Select(i => $"Col{i}"));
        var separator = string.Join(" | ", Enumerable.Repeat("---", columns));
        var body = string.Join(
            "\n",
            Enumerable.Range(0, rows).Select(_ => string.Join(" | ", Enumerable.Repeat(" ", columns))));
        var table = $"| {header} |\n| {separator} |\n" +
                    string.Join("\n", body.Split('\n').Select(r => $"|{r}|"));

        if (caret > 0 && text[caret - 1] != '\n')
        {
            table = "\n" + table;
        }

        table += "\n";
        var result = text.Insert(caret, table);
        return new EditResult(result, caret + table.Length, 0);
    }

    public static EditResult InsertTableOfContents(string text, int caret, string heading)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        heading = string.IsNullOrWhiteSpace(heading) ? "TOC" : heading.Trim();
        var outline = TocExtractor.Extract(text);
        var lines = outline.Select(node =>
        {
            var indent = new string(' ', Math.Max(0, node.Level - 1) * 2);
            return $"{indent}- [{node.Title}](#{node.Id})";
        });
        var body = outline.Count == 0
            ? $"## {heading}\n\n"
            : $"## {heading}\n\n" + string.Join('\n', lines) + "\n";
        if (caret > 0 && text[caret - 1] != '\n')
        {
            body = "\n" + body;
        }

        if (caret < text.Length && text[caret] != '\n')
        {
            body += "\n";
        }

        var rebuilt = text.Insert(caret, body);
        return new EditResult(rebuilt, caret + body.Length, 0);
    }

    public static EditResult InsertHorizontalRule(string text, int caret)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        var inserted = (caret > 0 && text[caret - 1] != '\n' ? "\n" : string.Empty) + "---\n";
        return new EditResult(text.Insert(caret, inserted), caret + inserted.Length, 0);
    }

    public static EditResult InsertDefinitionList(string text, int start, int length)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var term = length == 0 ? "Term" : text.Substring(start, length).Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            term = "Term";
        }

        var block = $"{term}\n: Definition";
        if (start > 0 && text[start - 1] != '\n')
        {
            block = "\n" + block;
        }

        if (start + length < text.Length && text[start + length] != '\n')
        {
            block += "\n";
        }

        var caret = start + block.LastIndexOf("Definition", StringComparison.Ordinal);
        return Replace(text, start, length, block, Math.Max(start, caret), "Definition".Length);
    }

    public static EditResult MoveLines(string text, int start, int length, int direction)
    {
        text ??= string.Empty;
        if (text.Length == 0 || direction == 0)
        {
            return new EditResult(text, start, length);
        }

        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        if (direction < 0)
        {
            if (first == 0)
            {
                return new EditResult(text, start, length);
            }

            var delta = map.OffsetOfLine(first) - map.OffsetOfLine(first - 1);
            var lines = Lines(map);
            var moved = lines[first - 1];
            for (var i = first; i <= last; i++)
            {
                lines[i - 1] = lines[i];
            }

            lines[last] = moved;
            return RebuildLines(lines, text, start - delta, length);
        }

        if (last >= map.LineCount - 1)
        {
            return new EditResult(text, start, length);
        }

        var downDelta = map.LineText(last + 1).Length + PreferredNewline(text).Length;
        var all = Lines(map);
        var swapped = all[last + 1];
        for (var i = last; i >= first; i--)
        {
            all[i + 1] = all[i];
        }

        all[first] = swapped;
        return RebuildLines(all, text, start + downDelta, length);
    }

    /// <summary>
    /// Wraps a selection with any pairable character. Without a selection only brackets and backticks
    /// are paired, and only at a word boundary, so apostrophes and emphasis markers type normally.
    /// </summary>
    public static EditResult? TryAutoPair(string text, int start, int length, char typed)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var next = start < text.Length ? text[start] : '\0';
        if (length == 0 && typed is ')' or ']' or '}' && next == typed)
            return new EditResult(text, start + 1, 0);

        var closing = PairCloser(typed);
        if (closing is null)
            return null;
        if (length > 0)
        {
            var selected = text.Substring(start, length);
            var wrapped = typed + selected + closing;
            return Replace(text, start, length, wrapped, start + 1, selected.Length);
        }

        if (typed is not ('(' or '[' or '{' or '`'))
            return null;
        if (typed == '`' && next == '`')
            return new EditResult(text, start + 1, 0);
        var previous = start > 0 ? text[start - 1] : '\0';
        var boundaryAfter = next is '\0' or ')' or ']' or '}' or ',' or '.' or ';' or ':' || char.IsWhiteSpace(next);
        var boundaryBefore = typed != '`' || previous is '\0' or '(' or '[' or '{' || char.IsWhiteSpace(previous);
        if (!boundaryAfter || !boundaryBefore)
            return null;
        return Replace(text, start, 0, typed.ToString() + closing, start + 1, 0);
    }

    public static bool IsAutoPairKey(char typed) => typed is ')' or ']' or '}' || PairCloser(typed) is not null;

    public static char? PairCloser(char opening) => opening switch
    {
        '(' => ')',
        '[' => ']',
        '{' => '}',
        '"' => '"',
        '\'' => '\'',
        '`' => '`',
        '*' => '*',
        '_' => '_',
        '~' => '~',
        '$' => '$',
        _ => null
    };

    public static EditResult InsertTimestamp(string text, int start, int length, DateTimeOffset now)
        => InsertText(text, start, length, now.ToString("yyyy-MM-dd HH:mm"));

    public static EditResult? ContinueBlockOnEnter(string text, int caret)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        var map = new LineMap(text);
        var line = map.LineOfOffset(caret);
        var lineText = map.LineText(line);
        var span = map.LineSpan(line);
        if (caret < span.Start + lineText.Length || MarkdownFence.CodeLines(map)[line])
        {
            return null;
        }

        if (!TryListPrefix(lineText, out var indent, out var marker, out var rest, out var orderedNumber))
        {
            if (lineText.TrimStart().StartsWith("> ", StringComparison.Ordinal) || lineText.Trim() == ">")
            {
                var quoteIndent = lineText.Length - lineText.TrimStart().Length;
                var quotePrefix = lineText[..quoteIndent] + "> ";
                var quoteBody = lineText[Math.Min(quotePrefix.Length, lineText.Length)..];
                if (string.IsNullOrWhiteSpace(quoteBody) || lineText.Trim() == ">")
                {
                    return Replace(text, span.Start, lineText.Length, indentSpaces(quoteIndent), span.Start + quoteIndent, 0);
                }

                var quoteInsert = PreferredNewline(text) + quotePrefix;
                return Replace(text, caret, 0, quoteInsert, caret + quoteInsert.Length, 0);
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(rest))
        {
            return Replace(text, span.Start, lineText.Length, indent, span.Start + indent.Length, 0);
        }

        var nextMarker = orderedNumber is { } number ? $"{number + 1}{marker[^2]} " :
            marker.Contains('[') ? marker[..3] + " ] " : marker;
        var insertion = PreferredNewline(text) + indent + nextMarker;
        return Replace(text, caret, 0, insertion, caret + insertion.Length, 0);

        static string indentSpaces(int count) => count <= 0 ? string.Empty : new string(' ', count);
    }

    public static EditResult ToggleHtmlComment(string text, int start, int length)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        if (length > 0)
        {
            var selected = text.Substring(start, length);
            if (selected.StartsWith("<!--", StringComparison.Ordinal) && selected.TrimEnd().EndsWith("-->", StringComparison.Ordinal))
            {
                var inner = selected[4..];
                inner = inner.TrimEnd();
                if (inner.EndsWith("-->", StringComparison.Ordinal))
                {
                    inner = inner[..^3];
                }

                inner = inner.Trim();
                return Replace(text, start, length, inner, start, inner.Length);
            }

            var wrapped = "<!-- " + selected + " -->";
            return Replace(text, start, length, wrapped, start, wrapped.Length);
        }

        var map = new LineMap(text);
        var line = map.LineOfOffset(start);
        var lineText = map.LineText(line);
        var span = map.LineSpan(line);
        if (lineText.TrimStart().StartsWith("<!--", StringComparison.Ordinal) && lineText.TrimEnd().EndsWith("-->", StringComparison.Ordinal))
        {
            var unwrapped = StripHtmlComment(lineText);
            return Replace(text, span.Start, lineText.Length, unwrapped, span.Start, unwrapped.Length);
        }

        var commented = "<!-- " + lineText + " -->";
        return Replace(text, span.Start, lineText.Length, commented, span.Start, commented.Length);
    }

    public static EditResult SortSelectedLines(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var lines = Lines(map);
        var slice = lines.Skip(first).Take(last - first + 1).OrderBy(line => line, StringComparer.OrdinalIgnoreCase).ToList();
        for (var i = 0; i < slice.Count; i++)
        {
            lines[first + i] = slice[i];
        }

        return RebuildLines(lines, text, map.OffsetOfLine(first), slice.Sum(l => l.Length) + Math.Max(0, slice.Count - 1));
    }

    public static EditResult ReverseSelectedLines(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        if (first >= last)
        {
            return new EditResult(text, start, length);
        }

        var lines = Lines(map);
        var slice = lines.Skip(first).Take(last - first + 1).Reverse().ToList();
        for (var i = 0; i < slice.Count; i++)
        {
            lines[first + i] = slice[i];
        }

        return RebuildLines(lines, text, map.OffsetOfLine(first), slice.Sum(l => l.Length) + Math.Max(0, slice.Count - 1));
    }

    private static (int Start, int Length) CurrentWord(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var start = caret;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        var end = caret;
        while (end < text.Length && IsWordChar(text[end]))
        {
            end++;
        }

        return (start, end - start);
    }

    private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch is '_' or '\'';

    private static string StripHtmlComment(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("<!--", StringComparison.Ordinal))
        {
            trimmed = trimmed[4..];
        }

        if (trimmed.EndsWith("-->", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }

    private static bool TryListPrefix(string line, out string indent, out string marker, out string rest, out int? orderedNumber)
    {
        indent = string.Empty;
        marker = string.Empty;
        rest = line;
        orderedNumber = null;
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        indent = line[..i];
        var body = line[i..];
        if (body.StartsWith("- [ ] ", StringComparison.Ordinal) ||
            body.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase) ||
            body.StartsWith("* [ ] ", StringComparison.Ordinal) ||
            body.StartsWith("* [x] ", StringComparison.OrdinalIgnoreCase) ||
            body.StartsWith("+ [ ] ", StringComparison.Ordinal) ||
            body.StartsWith("+ [x] ", StringComparison.OrdinalIgnoreCase))
        {
            marker = body[..6];
            rest = body[6..];
            return true;
        }

        if (body.StartsWith("- ", StringComparison.Ordinal) ||
            body.StartsWith("* ", StringComparison.Ordinal) ||
            body.StartsWith("+ ", StringComparison.Ordinal))
        {
            marker = body[..2];
            rest = body[2..];
            return true;
        }

        var n = 0;
        var digits = 0;
        while (digits < body.Length && digits < 9 && char.IsAsciiDigit(body[digits]))
        {
            n = (n * 10) + (body[digits] - '0');
            digits++;
        }

        if (digits > 0 && digits + 1 < body.Length && body[digits] is '.' or ')' && body[digits + 1] == ' ')
        {
            orderedNumber = n;
            marker = body[..(digits + 2)];
            rest = body[(digits + 2)..];
            return true;
        }

        return false;
    }

    private static EditResult ToggleLinePrefix(string text, int start, int length, string prefix)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (firstLine, lastLine) = SelectedLines(map, start, length);
        if (AnyCodeLine(map, firstLine, lastLine))
        {
            return new EditResult(text, start, length);
        }

        var allPrefixed = true;
        for (var line = firstLine; line <= lastLine; line++)
        {
            if (!map.LineText(line).TrimStart(' ').StartsWith(prefix, StringComparison.Ordinal))
            {
                allPrefixed = false;
                break;
            }
        }

        return TransformLines(text, firstLine, lastLine, lineText =>
        {
            if (allPrefixed)
            {
                var indent = lineText.Length - lineText.TrimStart(' ').Length;
                return lineText[..indent] + lineText[(indent + prefix.Length)..];
            }

            return prefix + lineText;
        }, start, length);
    }

    private static EditResult TransformLines(
        string text,
        int firstLine,
        int lastLine,
        Func<string, string> transform,
        int start,
        int length)
    {
        (start, length) = Clamp(text, start, length);
        var map = new LineMap(text);
        firstLine = Math.Clamp(firstLine, 0, map.LineCount - 1);
        lastLine = Math.Clamp(lastLine, firstLine, map.LineCount - 1);
        var builder = new System.Text.StringBuilder(text.Length + 16);
        builder.Append(text, 0, map.OffsetOfLine(firstLine));
        var newStart = start;
        var newEnd = start + length;

        for (var line = firstLine; line <= lastLine; line++)
        {
            var span = map.LineSpan(line);
            var original = map.LineText(line);
            var updated = transform(original);
            var prefix = 0;
            while (prefix < original.Length && prefix < updated.Length && original[prefix] == updated[prefix])
            {
                prefix++;
            }

            var suffix = 0;
            while (suffix < original.Length - prefix && suffix < updated.Length - prefix &&
                   original[^(suffix + 1)] == updated[^(suffix + 1)])
            {
                suffix++;
            }

            var changeStart = span.Start + prefix;
            var removed = original.Length - prefix - suffix;
            var inserted = updated.Length - prefix - suffix;
            newStart += PositionDelta(start, changeStart, removed, inserted, length == 0);
            newEnd += PositionDelta(start + length, changeStart, removed, inserted, true);
            builder.Append(updated);
            builder.Append(text, span.Start + original.Length, span.Length - original.Length);
        }

        var end = map.OffsetOfLine(lastLine + 1);
        builder.Append(text, end, text.Length - end);
        return new EditResult(builder.ToString(), newStart, Math.Max(0, newEnd - newStart));
    }

    private static int PositionDelta(int position, int changeStart, int removed, int inserted, bool rightAffinity)
    {
        if (position < changeStart || position == changeStart && !rightAffinity)
        {
            return 0;
        }

        if (position >= changeStart + removed)
        {
            return inserted - removed;
        }

        return changeStart + (rightAffinity ? inserted : 0) - position;
    }

    private static string StripHeading(string line)
    {
        var level = HeadingLevel(line);
        return level == 0 ? line : line.TrimStart()[level..].TrimStart();
    }

    private static bool AnyCodeLine(LineMap map, int first, int last)
    {
        var code = MarkdownFence.CodeLines(map);
        for (var line = first; line <= last && line < code.Length; line++)
            if (code[line]) return true;
        return false;
    }

    private static string NormalizeListLine(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
        {
            i++;
        }

        var indent = line[..i];
        var body = line[i..];
        if (body.Length >= 6 &&
            (body[0] == '*' || body[0] == '+') &&
            body[1] == ' ' &&
            body[2] == '[' &&
            body[4] == ']' &&
            body[5] == ' ' &&
            body[3] is ' ' or 'x' or 'X')
        {
            return indent + "- [" + body[3] + "] " + body[6..];
        }

        if (body.StartsWith("* ", StringComparison.Ordinal) || body.StartsWith("+ ", StringComparison.Ordinal))
        {
            return indent + "- " + body[2..];
        }

        return line;
    }

    private static bool IsSetextUnderline(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length >= 3 && (trimmed.All(static c => c == '=') || trimmed.All(static c => c == '-'));
    }

    private static string PadSeparatorCell(string spec, int width)
    {
        spec = (spec ?? string.Empty).Trim();
        var left = spec.StartsWith(':');
        var right = spec.EndsWith(':') && spec.Length > 1;
        var dashes = Math.Max(3, width - (left ? 1 : 0) - (right ? 1 : 0));
        return (left ? ":" : string.Empty) + new string('-', dashes) + (right ? ":" : string.Empty);
    }

    private static bool IsListLine(string line, bool ordered)
    {
        var trimmed = line.TrimStart();
        if (ordered)
        {
            var i = 0;
            while (i < trimmed.Length && char.IsDigit(trimmed[i]))
            {
                i++;
            }

            return i > 0 && i < trimmed.Length && trimmed[i] is '.' or ')' && (i + 1 == trimmed.Length || trimmed[i + 1] == ' ');
        }

        return trimmed.StartsWith("- ", StringComparison.Ordinal) ||
               trimmed.StartsWith("* ", StringComparison.Ordinal) ||
               trimmed.StartsWith("+ ", StringComparison.Ordinal);
    }

    private static bool IsTaskLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Length >= 5 && trimmed[0] is '-' or '*' or '+' && trimmed[1] == ' ' &&
               (trimmed[2..].StartsWith("[ ]", StringComparison.Ordinal) || trimmed[2..].StartsWith("[x]", StringComparison.OrdinalIgnoreCase));
    }

    private static string StripList(string line)
    {
        var trimmed = line.TrimStart();
        var indent = line.Length - trimmed.Length;
        var rest = trimmed;

        if (IsTaskLine(rest) && rest.Length >= 6 && rest[5] == ' ')
        {
            rest = rest[6..];
        }
        else if (rest.StartsWith("- ", StringComparison.Ordinal) ||
                 rest.StartsWith("* ", StringComparison.Ordinal) ||
                 rest.StartsWith("+ ", StringComparison.Ordinal))
        {
            rest = rest[2..];
        }
        else
        {
            var i = 0;
            while (i < rest.Length && char.IsDigit(rest[i]))
            {
                i++;
            }

            if (i > 0 && i + 1 < rest.Length && rest[i] is '.' or ')' && rest[i + 1] == ' ')
            {
                rest = rest[(i + 2)..];
            }
        }

        return line[..indent] + rest;
    }

    private static (int Start, int Length) Clamp(string text, int start, int length)
    {
        start = Math.Clamp(start, 0, text.Length);
        length = Math.Clamp(length, 0, text.Length - start);
        return (start, length);
    }

    private static EditResult Replace(string text, int start, int length, string replacement, int newStart, int newLength)
        => new(text[..start] + replacement + text[(start + length)..], newStart, newLength);
}

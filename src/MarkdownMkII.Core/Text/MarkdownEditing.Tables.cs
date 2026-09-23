namespace MarkdownMkII.Core.Text;

public static partial class MarkdownEditing
{
    public static EditResult? AdvanceTableCell(string text, int start, int length, bool reverse)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!IsTableRow(map.LineText(line)) || MarkdownFence.CodeLines(map)[line])
        {
            return null;
        }

        while (line >= 0 && IsSeparatorRow(map.LineText(line)))
        {
            line += reverse ? -1 : 1;
            if (line < 0 || line >= map.LineCount || !IsTableRow(map.LineText(line)))
            {
                return null;
            }
        }

        var lineText = map.LineText(line);
        var cells = TableCellRanges(lineText);
        if (cells.Count == 0)
        {
            return null;
        }

        var lineStart = map.OffsetOfLine(line);
        var caretInLine = Math.Clamp(start - lineStart, 0, lineText.Length);
        var index = 0;
        for (var i = 0; i < cells.Count; i++)
        {
            if (caretInLine >= cells[i].Start)
            {
                index = i;
            }
        }

        if (!reverse)
        {
            if (index < cells.Count - 1)
            {
                var next = cells[index + 1];
                return new EditResult(text, lineStart + next.Start, next.Length);
            }

            var nextLine = line + 1;
            while (nextLine < map.LineCount && IsSeparatorRow(map.LineText(nextLine)))
            {
                nextLine++;
            }

            if (nextLine < map.LineCount && IsTableRow(map.LineText(nextLine)))
            {
                var nextCells = TableCellRanges(map.LineText(nextLine));
                if (nextCells.Count == 0)
                {
                    return null;
                }

                return new EditResult(text, map.OffsetOfLine(nextLine) + nextCells[0].Start, nextCells[0].Length);
            }

            var row = string.Join(" | ", Enumerable.Repeat(" ", cells.Count));
            var newline = PreferredNewline(text);
            var inserted = $"|{row}|" + newline;
            var prefixLength = 0;
            var insertAt = map.OffsetOfLine(line + 1);
            if (insertAt > text.Length)
            {
                insertAt = text.Length;
            }

            if (insertAt == text.Length && text.Length > 0 && text[^1] is not ('\r' or '\n'))
            {
                inserted = newline + inserted;
                prefixLength = newline.Length;
                insertAt = text.Length;
            }

            var rebuilt = text[..insertAt] + inserted + text[insertAt..];
            var newLineStart = insertAt + prefixLength;
            var newCells = TableCellRanges(inserted.Trim('\r', '\n'));
            var first = newCells.Count == 0 ? (Start: 1, Length: 0) : newCells[0];
            return new EditResult(rebuilt, newLineStart + first.Start, first.Length);
        }

        if (index > 0)
        {
            var previous = cells[index - 1];
            return new EditResult(text, lineStart + previous.Start, previous.Length);
        }

        var previousLine = line - 1;
        while (previousLine >= 0 && IsSeparatorRow(map.LineText(previousLine)))
        {
            previousLine--;
        }

        if (previousLine < 0 || !IsTableRow(map.LineText(previousLine)))
        {
            var first = cells[0];
            return new EditResult(text, lineStart + first.Start, first.Length);
        }

        var prevCells = TableCellRanges(map.LineText(previousLine));
        if (prevCells.Count == 0)
        {
            return null;
        }

        var last = prevCells[^1];
        return new EditResult(text, map.OffsetOfLine(previousLine) + last.Start, last.Length);
    }

    public static string HeadingAnchor(string title)
    {
        title ??= string.Empty;
        var chars = title.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ch is ' ' or '-' ? '-' : '\0')
            .Where(ch => ch != '\0');
        var slug = new string(chars.ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    public static EditResult? AddTableRow(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!IsTableRow(map.LineText(line)) || AnyCodeLine(map, line, line))
        {
            return null;
        }

        while (line < map.LineCount && IsSeparatorRow(map.LineText(line)))
        {
            line++;
        }

        if (line >= map.LineCount || !IsTableRow(map.LineText(line)))
        {
            return null;
        }

        var cells = TableCellRanges(map.LineText(line));
        var count = Math.Max(1, cells.Count);
        var inserted = "|" + string.Join(" | ", Enumerable.Repeat(" ", count)) + "|\n";
        var insertAt = map.OffsetOfLine(line + 1);
        if (insertAt > text.Length)
        {
            insertAt = text.Length;
        }

        if (insertAt == text.Length && text.Length > 0 && text[^1] != '\n')
        {
            inserted = "\n" + inserted;
        }

        var rebuilt = text[..insertAt] + inserted + text[insertAt..];
        var newLine = insertAt + (inserted[0] == '\n' ? 1 : 0);
        var newCells = TableCellRanges(inserted.Trim('\n'));
        var first = newCells.Count == 0 ? (Start: 1, Length: 0) : newCells[0];
        return new EditResult(rebuilt, newLine + first.Start, first.Length);
    }

    public static EditResult? DuplicateTableRow(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var raw = map.LineText(line);
        if (!IsTableRow(raw) || IsSeparatorRow(raw) || AnyCodeLine(map, line, line))
        {
            return null;
        }

        var copy = raw + "\n";
        var insertAt = map.OffsetOfLine(line + 1);
        if (insertAt > text.Length)
        {
            insertAt = text.Length;
        }

        if (insertAt == text.Length && text.Length > 0 && text[^1] != '\n')
        {
            copy = "\n" + copy;
        }

        var rebuilt = text[..insertAt] + copy + text[insertAt..];
        var caret = insertAt + (copy[0] == '\n' ? 1 : 0);
        return new EditResult(rebuilt, caret, raw.Length);
    }

    public static EditResult? DuplicateListItem(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var raw = map.LineText(line);
        if (!TryListPrefix(raw, out _, out _, out _, out _))
        {
            return null;
        }

        var copy = raw + "\n";
        var insertAt = map.OffsetOfLine(line + 1);
        if (insertAt == text.Length && text.Length > 0 && text[^1] != '\n')
        {
            copy = "\n" + copy;
        }

        var rebuilt = text[..insertAt] + copy + text[insertAt..];
        var caret = insertAt + (copy[0] == '\n' ? 1 : 0);
        return new EditResult(rebuilt, caret, raw.Length);
    }

    public static EditResult? MoveListItem(string text, int start, int length, int direction)
    {
        if (direction == 0)
        {
            return new EditResult(text ?? string.Empty, start, length);
        }

        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        var lines = Lines(map);
        if (line < 0 || line >= lines.Count || !TryListPrefix(lines[line], out var indent, out _, out _, out _))
        {
            return null;
        }

        var target = line + (direction < 0 ? -1 : 1);
        if (target < 0 || target >= lines.Count ||
            !TryListPrefix(lines[target], out var targetIndent, out _, out _, out _) ||
            indent != targetIndent)
        {
            return null;
        }

        (lines[line], lines[target]) = (lines[target], lines[line]);
        return RebuildLines(lines, text, map.OffsetOfLine(target), lines[target].Length);
    }

    public static EditResult? DeleteListItem(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!TryListPrefix(map.LineText(line), out _, out _, out _, out _))
        {
            return null;
        }

        var span = map.LineSpan(line);
        var rebuilt = text[..span.Start] + (span.Start + span.Length < text.Length
            ? text[(span.Start + span.Length)..]
            : string.Empty);
        return new EditResult(rebuilt, Math.Min(span.Start, rebuilt.Length), 0);
    }

    public static EditResult? DeleteTableRow(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!IsTableRow(map.LineText(line)) || IsSeparatorRow(map.LineText(line)))
        {
            return null;
        }

        var block = TableLineRange(map, line);
        if (block is null || block.Value.Last - block.Value.First < 2)
        {
            return null;
        }

        var span = map.LineSpan(line);
        var rebuilt = text[..span.Start] + text[(span.Start + span.Length)..];
        var caret = Math.Min(span.Start, rebuilt.Length);
        return new EditResult(rebuilt, caret, 0);
    }

    public static EditResult? AddTableColumn(string text, int start, int length)
    {
        return MutateTable(text, start, (map, first, last, _) =>
        {
            var lines = Lines(map);
            for (var i = first; i <= last; i++)
            {
                var line = lines[i];
                if (!line.TrimEnd().EndsWith('|'))
                {
                    line += " |";
                }

                lines[i] = IsSeparatorRow(line)
                    ? line.TrimEnd().TrimEnd('|') + " --- |"
                    : line.TrimEnd().TrimEnd('|') + "  |";
            }

            return RebuildLines(lines, text, start, 0);
        });
    }

    public static EditResult? DuplicateTableColumn(string text, int start, int length)
    {
        return MutateTable(text, start, (map, first, last, column) =>
        {
            if (column < 0)
            {
                return new EditResult(text, start, length);
            }

            var lines = Lines(map);
            for (var i = first; i <= last; i++)
            {
                var cells = SplitTableRow(lines[i]);
                if (column >= cells.Count)
                {
                    return new EditResult(text, start, length);
                }

                cells.Insert(column + 1, cells[column]);
                lines[i] = JoinTableRow(cells, IsSeparatorRow(lines[i]));
            }

            return RebuildLines(lines, text, start, 0);
        });
    }

    public static EditResult? DeleteTableColumn(string text, int start, int length)
    {
        return MutateTable(text, start, (map, first, last, column) =>
        {
            var lines = Lines(map);
            for (var i = first; i <= last; i++)
            {
                var cells = SplitTableRow(lines[i]);
                if (cells.Count <= 1 || column < 0 || column >= cells.Count)
                {
                    continue;
                }

                cells.RemoveAt(column);
                lines[i] = JoinTableRow(cells, IsSeparatorRow(lines[i]));
            }

            return RebuildLines(lines, text, start, 0);
        });
    }

    public static EditResult? MoveTableColumn(string text, int start, int length, int direction)
    {
        if (direction == 0)
        {
            return new EditResult(text ?? string.Empty, start, length);
        }

        return MutateTable(text, start, (map, first, last, column) =>
        {
            var target = column + direction;
            if (target < 0)
            {
                return new EditResult(text, start, length);
            }

            var lines = Lines(map);
            for (var i = first; i <= last; i++)
            {
                var cells = SplitTableRow(lines[i]);
                if (column < 0 || column >= cells.Count || target < 0 || target >= cells.Count)
                {
                    return new EditResult(text, start, length);
                }

                (cells[column], cells[target]) = (cells[target], cells[column]);
                lines[i] = JoinTableRow(cells, IsSeparatorRow(lines[i]));
            }

            return RebuildLines(lines, text, start, 0);
        });
    }

    public static EditResult? MoveTableRow(string text, int start, int length, int direction)
    {
        if (direction == 0)
        {
            return new EditResult(text ?? string.Empty, start, length);
        }

        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!IsTableRow(map.LineText(line)) || IsSeparatorRow(map.LineText(line)))
        {
            return null;
        }

        var block = TableLineRange(map, line);
        if (block is null)
        {
            return null;
        }

        var sep = -1;
        for (var i = block.Value.First; i <= block.Value.Last; i++)
        {
            if (IsSeparatorRow(map.LineText(i)))
            {
                sep = i;
                break;
            }
        }

        var bodyFirst = sep >= 0 ? sep + 1 : block.Value.First + 1;
        if (line < bodyFirst || line > block.Value.Last)
        {
            return null;
        }

        var target = line + direction;
        if (target == sep)
        {
            target += direction;
        }

        if (target < bodyFirst || target > block.Value.Last)
        {
            return null;
        }

        var lines = Lines(map);
        (lines[line], lines[target]) = (lines[target], lines[line]);
        return RebuildLines(lines, text, map.OffsetOfLine(target), 0);
    }

    public static EditResult? AlignTableColumn(string text, int start, int length, string align)
    {
        align = (align ?? "left").Trim().ToLowerInvariant();
        var spec = align switch
        {
            "center" => ":---:",
            "right" => "---:",
            _ => ":---"
        };

        return MutateTable(text, start, (map, first, last, column) =>
        {
            var lines = Lines(map);
            for (var i = first; i <= last; i++)
            {
                if (!IsSeparatorRow(lines[i]))
                {
                    continue;
                }

                var cells = SplitTableRow(lines[i]);
                if (column < 0 || column >= cells.Count)
                {
                    return new EditResult(text, start, length);
                }

                cells[column] = spec;
                lines[i] = JoinTableRow(cells, separator: true);
            }

            return RebuildLines(lines, text, start, 0);
        });
    }

    public static EditResult? SortTableColumn(string text, int start, int length)
        => SortTableColumn(text, start, length, descending: false);

    public static EditResult? SortTableColumn(string text, int start, int length, bool descending)
    {
        return MutateTable(text, start, (map, first, last, column) =>
        {
            var lines = Lines(map);
            var bodyStart = first + 1;
            if (bodyStart <= last && IsSeparatorRow(lines[bodyStart]))
            {
                bodyStart++;
            }

            if (bodyStart > last || column < 0)
            {
                return new EditResult(text, start, length);
            }

            var rows = new List<(int Index, string Line, string Key)>();
            for (var i = bodyStart; i <= last; i++)
            {
                var cells = SplitTableRow(lines[i]);
                var key = column < cells.Count ? cells[column].Trim() : string.Empty;
                rows.Add((i, lines[i], key));
            }

            if (rows.Count == 0)
            {
                return new EditResult(text, start, length);
            }

            var numeric = rows.TrueForAll(row =>
                double.TryParse(row.Key, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _));
            IOrderedEnumerable<(int Index, string Line, string Key)> ordered;
            if (numeric)
            {
                ordered = descending
                    ? rows.OrderByDescending(row => double.Parse(row.Key, System.Globalization.CultureInfo.InvariantCulture))
                        .ThenBy(row => row.Index)
                    : rows.OrderBy(row => double.Parse(row.Key, System.Globalization.CultureInfo.InvariantCulture))
                        .ThenBy(row => row.Index);
            }
            else
            {
                ordered = descending
                    ? rows.OrderByDescending(row => row.Key, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row => row.Index)
                    : rows.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(row => row.Index);
            }

            var rebuilt = lines;
            var cursor = bodyStart;
            foreach (var row in ordered)
            {
                rebuilt[cursor++] = row.Line;
            }

            return RebuildLines(rebuilt, text, start, 0);
        });
    }

    public static EditResult? TransposeTable(string text, int start, int length)
    {
        return MutateTable(text, start, (map, first, last, _) =>
        {
            var lines = Lines(map);
            var rows = new List<List<string>>();
            var sepAt = -1;
            for (var i = first; i <= last; i++)
            {
                if (IsSeparatorRow(lines[i]))
                {
                    sepAt = rows.Count;
                    continue;
                }

                rows.Add(SplitTableRow(lines[i]));
            }

            if (rows.Count < 2)
            {
                return new EditResult(text, start, length);
            }

            var width = rows.Max(row => row.Count);
            if (width < 1)
            {
                return new EditResult(text, start, length);
            }

            foreach (var row in rows)
            {
                while (row.Count < width)
                {
                    row.Add(string.Empty);
                }
            }

            var transposed = new List<string>();
            for (var column = 0; column < width; column++)
            {
                var cells = rows.Select(row => row[column]).ToList();
                if (column == 1 && sepAt >= 0)
                {
                    transposed.Add(JoinTableRow(Enumerable.Repeat("---", rows.Count).ToList(), separator: true));
                }

                transposed.Add(JoinTableRow(cells, separator: false));
            }

            if (sepAt < 0)
            {
                transposed.Insert(1, JoinTableRow(Enumerable.Repeat("---", rows.Count).ToList(), separator: true));
            }

            var rebuilt = lines.Take(first).Concat(transposed).Concat(lines.Skip(last + 1)).ToList();
            return RebuildLines(rebuilt, text, map.OffsetOfLine(first), 0);
        });
    }

    public static string? TableToDelimited(string text, int start, char separator = ',')
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!IsTableRow(map.LineText(line)))
        {
            return null;
        }

        var block = TableLineRange(map, line);
        if (block is null)
        {
            return null;
        }

        var rows = new List<string>();
        for (var i = block.Value.First; i <= block.Value.Last; i++)
        {
            if (IsSeparatorRow(map.LineText(i)))
            {
                continue;
            }

            var cells = SplitTableRow(map.LineText(i));
            rows.Add(string.Join(separator, cells.Select(cell => QuoteDelimited(cell, separator))));
        }

        return rows.Count == 0 ? null : string.Join('\n', rows) + "\n";
    }

    public static string? TableToHtml(string text, int start)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!IsTableRow(map.LineText(line)))
        {
            return null;
        }

        var block = TableLineRange(map, line);
        if (block is null)
        {
            return null;
        }

        var rows = new List<List<string>>();
        for (var i = block.Value.First; i <= block.Value.Last; i++)
        {
            if (IsSeparatorRow(map.LineText(i)))
            {
                continue;
            }

            rows.Add(SplitTableRow(map.LineText(i)));
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("<table>");
        for (var i = 0; i < rows.Count; i++)
        {
            if (i == 0)
            {
                builder.AppendLine("<thead>");
            }
            else if (i == 1)
            {
                builder.AppendLine("<tbody>");
            }

            var tag = i == 0 ? "th" : "td";
            builder.Append("<tr>");
            foreach (var cell in rows[i])
            {
                builder.Append('<').Append(tag).Append('>')
                    .Append(System.Net.WebUtility.HtmlEncode(cell))
                    .Append("</").Append(tag).Append('>');
            }

            builder.AppendLine("</tr>");
            if (i == 0)
            {
                builder.AppendLine("</thead>");
            }
        }

        if (rows.Count > 1)
        {
            builder.AppendLine("</tbody>");
        }

        builder.AppendLine("</table>");
        return builder.ToString();
    }

    public static EditResult? FormatTable(string text, int start, int length)
    {
        return MutateTable(text, start, (map, first, last, _) =>
        {
            var lines = Lines(map);
            var parsed = new List<(int Index, List<string> Cells, bool Separator)>();
            var width = 0;
            for (var i = first; i <= last; i++)
            {
                if (!IsTableRow(lines[i]))
                {
                    continue;
                }

                var cells = SplitTableRow(lines[i]);
                width = Math.Max(width, cells.Count);
                parsed.Add((i, cells, IsSeparatorRow(lines[i])));
            }

            if (parsed.Count == 0 || width == 0)
            {
                return new EditResult(text, start, length);
            }

            foreach (var row in parsed)
            {
                while (row.Cells.Count < width)
                {
                    row.Cells.Add(string.Empty);
                }
            }

            var colWidths = new int[width];
            foreach (var row in parsed)
            {
                for (var c = 0; c < width; c++)
                {
                    var cell = row.Separator ? PadSeparatorCell(row.Cells[c], 3) : row.Cells[c];
                    colWidths[c] = Math.Max(colWidths[c], Math.Max(3, cell.Length));
                }
            }

            foreach (var row in parsed)
            {
                for (var c = 0; c < width; c++)
                {
                    row.Cells[c] = row.Separator
                        ? PadSeparatorCell(row.Cells[c], colWidths[c])
                        : row.Cells[c].PadRight(colWidths[c]);
                }

                lines[row.Index] = JoinTableRow(row.Cells, row.Separator);
            }

            return RebuildLines(lines, text, start, 0);
        });
    }

    public static EditResult InsertTableFromDelimited(string text, int start, int length, string source)
    {
        text ??= string.Empty;
        (start, length) = Clamp(text, start, length);
        var rows = ParseDelimited(source);
        return rows.Count == 0 ? new EditResult(text, start, length) :
            InsertText(text, start, length, MarkdownTables.FromRows(rows));
    }

    public static EditResult? TableToList(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!IsTableRow(map.LineText(line)))
        {
            return null;
        }

        var block = TableLineRange(map, line);
        if (block is null)
        {
            return null;
        }

        var bullets = new List<string>();
        for (var i = block.Value.First; i <= block.Value.Last; i++)
        {
            if (IsSeparatorRow(map.LineText(i)))
            {
                continue;
            }

            var cells = SplitTableRow(map.LineText(i));
            if (i == block.Value.First)
            {
                continue;
            }

            var body = string.Join(" — ", cells.Where(cell => cell.Length > 0));
            if (body.Length == 0)
            {
                continue;
            }

            bullets.Add("- " + body);
        }

        if (bullets.Count == 0)
        {
            return null;
        }

        var first = map.LineSpan(block.Value.First);
        var last = map.LineSpan(block.Value.Last);
        var markdown = string.Join('\n', bullets) + "\n";
        return InsertText(text, first.Start, last.Start + last.Length - first.Start, markdown);
    }

    public static EditResult? ListToTable(string text, int start, int length)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var (first, last) = SelectedLines(map, start, length);
        var rows = new List<List<string>>();
        for (var i = first; i <= last; i++)
        {
            if (!TryListPrefix(map.LineText(i), out _, out _, out var rest, out _))
            {
                continue;
            }

            rows.Add(SplitListCells(rest));
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var width = rows.Max(row => row.Count);
        var header = width <= 1
            ? new List<string> { "Item" }
            : Enumerable.Range(1, width).Select(index => "Col" + index).ToList();
        var source = string.Join(
            '\n',
            new[] { string.Join(',', header) }.Concat(rows.Select(row =>
            {
                while (row.Count < width)
                {
                    row.Add(string.Empty);
                }

                return string.Join(',', row.Select(cell => QuoteDelimited(cell, ',')));
            })));
        var table = InsertTableFromDelimited(string.Empty, 0, 0, source).Text;
        var firstSpan = map.LineSpan(first);
        var lastSpan = map.LineSpan(last);
        return InsertText(text, firstSpan.Start, lastSpan.Start + lastSpan.Length - firstSpan.Start, table);
    }

    private static List<string> SplitListCells(string rest)
    {
        rest = (rest ?? string.Empty).Trim();
        if (rest.Contains('|', StringComparison.Ordinal))
        {
            return rest.Split('|', StringSplitOptions.TrimEntries).Where(cell => cell.Length > 0).ToList();
        }

        if (rest.Contains('\t') || rest.Contains(','))
        {
            var parsed = ParseDelimited(rest);
            return parsed.Count == 0 ? [rest] : parsed[0];
        }

        return rest.Length == 0 ? [string.Empty] : [rest];
    }

    private static EditResult? MutateTable(
        string text,
        int start,
        Func<LineMap, int, int, int, EditResult> mutate)
    {
        text ??= string.Empty;
        var map = new LineMap(text);
        var line = map.LineOfOffset(Math.Clamp(start, 0, text.Length));
        if (!IsTableRow(map.LineText(line)))
        {
            return null;
        }

        var block = TableLineRange(map, line);
        if (block is null)
        {
            return null;
        }

        var lineStart = map.OffsetOfLine(line);
        var caretInLine = Math.Clamp(start - lineStart, 0, map.LineText(line).Length);
        var cells = TableCellRanges(map.LineText(line));
        var column = 0;
        for (var i = 0; i < cells.Count; i++)
        {
            if (caretInLine >= cells[i].Start)
            {
                column = i;
            }
        }

        return mutate(map, block.Value.First, block.Value.Last, column);
    }

    private static (int First, int Last)? TableLineRange(LineMap map, int line)
    {
        if (!IsTableRow(map.LineText(line)) || MarkdownFence.CodeLines(map)[line])
        {
            return null;
        }

        var first = line;
        while (first > 0 && IsTableRow(map.LineText(first - 1)))
        {
            first--;
        }

        var last = line;
        while (last + 1 < map.LineCount && IsTableRow(map.LineText(last + 1)))
        {
            last++;
        }

        return (first, last);
    }

    private static List<string> SplitTableRow(string line)
        => TableCellRanges(line).Select(cell => line.Substring(cell.Start, cell.Length)).ToList();

    private static string JoinTableRow(IReadOnlyList<string> cells, bool separator)
    {
        if (separator)
        {
            return "| " + string.Join(" | ", cells.Select(cell => cell.Length == 0 ? "---" : cell)) + " |";
        }

        return "| " + string.Join(" | ", cells) + " |";
    }

    private static string QuoteDelimited(string cell, char separator)
    {
        cell ??= string.Empty;
        if (cell.IndexOfAny(['"', '\n', '\r', separator]) < 0)
        {
            return cell;
        }

        return "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static List<List<string>> ParseDelimited(string source)
        => DelimitedText.Parse(source, trimCells: true);

    private static bool IsTableRow(string line)
        => line.TrimStart().StartsWith('|') && TableCellRanges(line).Count > 0;

    private static bool IsSeparatorRow(string line)
    {
        if (!IsTableRow(line))
        {
            return false;
        }

        return SplitTableRow(line).All(cell =>
        {
            var dashes = cell.Trim(':');
            return dashes.Length >= 3 && dashes.All(ch => ch == '-');
        });
    }

    private static List<(int Start, int Length)> TableCellRanges(string line)
    {
        var delimiters = new List<int>();
        var backslashes = 0;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '|' && backslashes % 2 == 0)
            {
                delimiters.Add(i);
            }

            backslashes = line[i] == '\\' ? backslashes + 1 : 0;
        }

        var ranges = new List<(int Start, int Length)>();
        if (delimiters.Count == 0)
        {
            return ranges;
        }

        var start = 0;
        foreach (var delimiter in delimiters)
        {
            if (delimiter == delimiters[0] && string.IsNullOrWhiteSpace(line[..delimiter]))
            {
                start = delimiter + 1;
                continue;
            }

            AddCell(start, delimiter);
            start = delimiter + 1;
        }

        if (!string.IsNullOrWhiteSpace(line[start..]))
        {
            AddCell(start, line.Length);
        }

        return ranges;

        void AddCell(int from, int to)
        {
            while (from < to && char.IsWhiteSpace(line[from]))
            {
                from++;
            }

            while (to > from && char.IsWhiteSpace(line[to - 1]))
            {
                to--;
            }

            ranges.Add((from, to - from));
        }
    }
}

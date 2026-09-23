using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public static class MarkdownTables
{
    public static string FromRows(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return string.Empty;
        }

        var width = rows.Max(row => row.Count);
        if (width < 1)
        {
            return string.Empty;
        }

        var padded = rows.Select(row =>
        {
            var cells = row.Select(EscapeCell).ToList();
            while (cells.Count < width)
            {
                cells.Add(string.Empty);
            }

            return cells;
        }).ToList();

        var builder = new StringBuilder();
        AppendRow(builder, padded[0]);
        AppendRow(builder, Enumerable.Repeat("---", width));
        for (var i = 1; i < padded.Count; i++)
        {
            AppendRow(builder, padded[i]);
        }

        return builder.ToString();
    }

    public static bool TryFromDelimited(string text, out string markdown)
    {
        markdown = string.Empty;
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0 || LooksLikeMarkdownTable(text))
        {
            return false;
        }

        return TryRows(DelimitedText.Parse(text), out markdown);
    }

    public static string FromHtml(string html)
    {
        html ??= string.Empty;
        var rows = new List<IReadOnlyList<string>>();
        foreach (Match row in Regex.Matches(
                     html,
                     @"<tr[\s\S]*?</tr>",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                     TimeSpan.FromMilliseconds(80)))
        {
            var cells = new List<string>();
            foreach (Match cell in Regex.Matches(
                         row.Value,
                         @"<t[hd][^>]*>([\s\S]*?)</t[hd]>",
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                         TimeSpan.FromMilliseconds(80)))
            {
                cells.Add(HtmlToMarkdown.Convert(cell.Groups[1].Value).Replace('\n', ' ').Replace('\r', ' ').Trim());
            }

            if (cells.Count > 0)
            {
                rows.Add(cells);
            }
        }

        return rows.Count == 0 ? string.Empty : FromRows(rows);
    }

    private static bool TryRows(IReadOnlyList<IReadOnlyList<string>> rows, out string markdown)
    {
        markdown = string.Empty;
        if (rows.Count < 2 || rows.Any(row => row.Count < 2) || rows.Max(row => row.Count) < 2)
        {
            return false;
        }

        markdown = FromRows(rows);
        return markdown.Length > 0;
    }

    private static bool LooksLikeMarkdownTable(string text)
        => text.Contains('|', StringComparison.Ordinal) &&
           text.Contains("---", StringComparison.Ordinal);
    private static void AppendRow(StringBuilder builder, IEnumerable<string> cells)
        => builder.Append("| ").Append(string.Join(" | ", cells)).Append(" |\n");

    private static string EscapeCell(string cell)
        => (cell ?? string.Empty)
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Trim();
}

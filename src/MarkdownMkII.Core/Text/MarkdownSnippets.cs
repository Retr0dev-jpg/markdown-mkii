namespace MarkdownMkII.Core.Text;

public static class MarkdownSnippets
{
    public static EditResult? TryExpand(string text, int caret, DateTimeOffset? now = null, string? fileName = null)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        var start = caret;
        while (start > 0 && IsTokenChar(text[start - 1]))
        {
            start--;
        }

        if (start >= caret || text[start] != ':')
        {
            return null;
        }

        var token = text[start..caret];
        var stamp = now ?? DateTimeOffset.Now;
        var replacement = token.ToLowerInvariant() switch
        {
            ":date" => stamp.ToString("yyyy-MM-dd"),
            ":time" => stamp.ToString("HH:mm"),
            ":now" => stamp.ToString("yyyy-MM-dd HH:mm"),
            ":uuid" => Guid.NewGuid().ToString("N")[..8],
            ":year" => stamp.Year.ToString(),
            ":week" => System.Globalization.ISOWeek.GetYear(stamp.DateTime)
                + "-W"
                + System.Globalization.ISOWeek.GetWeekOfYear(stamp.DateTime).ToString("D2"),
            ":filename" => string.IsNullOrWhiteSpace(fileName)
                ? null
                : Path.GetFileNameWithoutExtension(fileName),
            _ => null
        };
        if (replacement is null)
        {
            return null;
        }

        var next = text[..start] + replacement + text[caret..];
        return new EditResult(next, start, replacement.Length);
    }

    private static bool IsTokenChar(char ch) => char.IsLetterOrDigit(ch) || ch is ':' or '_' or '-';
}

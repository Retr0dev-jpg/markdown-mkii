namespace MarkdownMkII.Core.Text;

public readonly record struct DiffLine(char Kind, string Text, int LeftLine, int RightLine);

public static class TextDiff
{
    public static IReadOnlyList<DiffLine> Lines(string? left, string right)
    {
        var a = Split(left);
        var b = Split(right);
        if (a.Length == 0 && b.Length == 0)
        {
            return [];
        }

        if (a.Length > 800 || b.Length > 800)
        {
            return Coarse(a, b);
        }

        return Lcs(a, b);
    }

    public static string Summarize(string? left, string right, int maxLines = 16)
    {
        var lines = Lines(left, right);
        var added = lines.Count(line => line.Kind == '+');
        var removed = lines.Count(line => line.Kind == '-');
        var builder = new System.Text.StringBuilder();
        builder.Append('+').Append(added).Append(" −").Append(removed);
        var shown = 0;
        foreach (var line in lines)
        {
            if (line.Kind == ' ')
            {
                continue;
            }

            if (shown >= maxLines)
            {
                builder.Append("\n…");
                break;
            }

            builder.Append('\n').Append(line.Kind).Append(' ').Append(line.Text);
            shown++;
        }

        return builder.ToString();
    }

    private static IReadOnlyList<DiffLine> Lcs(string[] a, string[] b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i < a.Length; i++)
        {
            for (var j = 0; j < b.Length; j++)
            {
                dp[i + 1, j + 1] = string.Equals(a[i], b[j], StringComparison.Ordinal)
                    ? dp[i, j] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var result = new List<DiffLine>(a.Length + b.Length);
        var iPos = a.Length;
        var jPos = b.Length;
        while (iPos > 0 || jPos > 0)
        {
            if (iPos > 0 && jPos > 0 && string.Equals(a[iPos - 1], b[jPos - 1], StringComparison.Ordinal))
            {
                result.Add(new DiffLine(' ', a[iPos - 1], iPos - 1, jPos - 1));
                iPos--;
                jPos--;
            }
            else if (jPos > 0 && (iPos == 0 || dp[iPos, jPos - 1] >= dp[iPos - 1, jPos]))
            {
                result.Add(new DiffLine('+', b[jPos - 1], iPos, jPos - 1));
                jPos--;
            }
            else
            {
                result.Add(new DiffLine('-', a[iPos - 1], iPos - 1, jPos));
                iPos--;
            }
        }

        result.Reverse();
        return result;
    }

    public static string ToUnified(string? left, string right, string? path = null)
    {
        var name = string.IsNullOrWhiteSpace(path) ? "note.md" : path.Trim().Replace('\\', '/');
        var builder = new System.Text.StringBuilder();
        builder.Append("--- a/").Append(name).Append('\n');
        builder.Append("+++ b/").Append(name).Append('\n');
        foreach (var line in Lines(left, right))
        {
            builder.Append(line.Kind == ' ' ? ' ' : line.Kind).Append(line.Text).Append('\n');
        }

        return builder.ToString();
    }

    private static IReadOnlyList<DiffLine> Coarse(string[] a, string[] b)
    {
        // Keep a linear fallback for large documents while retaining duplicate and reordered lines.
        var prefix = 0;
        while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < a.Length - prefix && suffix < b.Length - prefix && a[^(suffix + 1)] == b[^(suffix + 1)])
        {
            suffix++;
        }

        var result = new List<DiffLine>(a.Length + b.Length);
        for (var i = 0; i < prefix; i++)
        {
            result.Add(new DiffLine(' ', a[i], i, i));
        }

        for (var i = prefix; i < a.Length - suffix; i++)
        {
            result.Add(new DiffLine('-', a[i], i, prefix));
        }

        for (var i = prefix; i < b.Length - suffix; i++)
        {
            result.Add(new DiffLine('+', b[i], a.Length - suffix, i));
        }

        for (var i = suffix; i > 0; i--)
        {
            result.Add(new DiffLine(' ', a[^i], a.Length - i, b.Length - i));
        }

        return result;
    }

    private static string[] Split(string? text)
        => string.IsNullOrEmpty(text)
            ? []
            : text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
}

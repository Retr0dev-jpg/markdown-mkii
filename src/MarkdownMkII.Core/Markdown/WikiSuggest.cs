using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Markdown;

public static class WikiSuggest
{
    public static string? PartialTarget(string text, int caret)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        var open = ActiveOpening(text, caret);
        if (open < 0)
        {
            return null;
        }

        var inner = text[(open + 2)..caret];
        return inner[..TargetEnd(inner)].Trim();
    }

    public static IReadOnlyList<string> Filter(IReadOnlyList<string> catalog, string? query, int max = 12)
    {
        max = Math.Clamp(max, 1, 40);
        var matches = PaletteFilter.Apply(catalog ?? [], item => item, query ?? string.Empty);
        if (string.IsNullOrWhiteSpace(query))
        {
            return matches.Take(max).ToList();
        }

        return matches
            .OrderBy(item => Rank(item, query))
            .ThenBy(item => item, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
    }

    public static string? Pick(IReadOnlyList<string> suggestions, string? query)
    {
        if (suggestions is null || suggestions.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var exact = suggestions.FirstOrDefault(item =>
                string.Equals(item, query, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        return suggestions[0];
    }

    public static EditResult Complete(string text, int caret, string target)
    {
        text ??= string.Empty;
        target = (target ?? string.Empty).Trim();
        caret = Math.Clamp(caret, 0, text.Length);
        if (target.Length == 0)
        {
            return new EditResult(text, caret, 0);
        }

        var open = ActiveOpening(text, caret);
        if (open < 0)
        {
            return MarkdownEditing.InsertWikilink(text, caret, 0, target);
        }

        var close = text.IndexOf("]]", open, StringComparison.Ordinal);
        if (close >= 0 && text.AsSpan(open, close - open).IndexOfAny('\r', '\n') >= 0)
        {
            close = -1;
        }
        var innerEnd = close >= caret ? close : caret;
        var inner = text[(open + 2)..innerEnd];
        var suffix = inner[TargetEnd(inner)..];
        var rest = close >= caret ? text[(close + 2)..] : text[caret..];
        var rebuilt = text[..(open + 2)] + target + suffix + "]]" + rest;
        return new EditResult(rebuilt, open + 2, target.Length);
    }

    private static int ActiveOpening(string text, int caret)
    {
        // Only consider a complete opening delimiter before the insertion point.
        var beforeCaret = text.AsSpan(0, caret);
        var open = beforeCaret.LastIndexOf("[[", StringComparison.Ordinal);
        if (open < 0)
        {
            return -1;
        }

        var inner = beforeCaret[(open + 2)..];
        return inner.IndexOfAny('\r', '\n') >= 0 || inner.Contains("]]", StringComparison.Ordinal)
            || (caret < text.Length && text[caret] == ']' && caret > open + 2 && text[caret - 1] == ']')
            ? -1
            : open;
    }

    private static int TargetEnd(string inner)
    {
        var separator = inner.AsSpan().IndexOfAny('#', '|');
        return separator < 0 ? inner.Length : separator;
    }

    private static int Rank(string item, string query)
    {
        if (item.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (item.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }
}

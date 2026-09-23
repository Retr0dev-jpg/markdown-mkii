namespace MarkdownMkII.Core.Text;

public static class PaletteFilter
{
    public static bool Matches(string label, string query)
    {
        label ??= string.Empty;
        query ??= string.Empty;
        if (query.Length == 0)
        {
            return true;
        }

        foreach (var word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!label.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static bool MatchesAny(string query, params string?[] fields)
    {
        var haystack = string.Join(' ', fields.Where(field => !string.IsNullOrWhiteSpace(field)));
        return Matches(haystack, query);
    }

    public static IReadOnlyList<T> Apply<T>(IEnumerable<T> items, Func<T, string> label, string query)
        => items.Where(item => Matches(label(item), query)).ToList();
}

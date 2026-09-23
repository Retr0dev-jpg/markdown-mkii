using System.Globalization;
using System.Text;

namespace MarkdownMkII.Core.Editor;
public sealed record CommandSearchEntry(string Id, string Label, string Category, string Keywords, bool Available = true);
public static class CommandSearch
{
    private static string Fold(string value) => string.Concat(value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
    public static IReadOnlyList<string> Find(IEnumerable<CommandSearchEntry> entries, string query, IReadOnlyList<string> recents, IReadOnlyList<string> frequent)
    {
        var source = entries.Where(e => e.Available).ToArray();
        if (string.IsNullOrWhiteSpace(query))
        {
            var available = source.Select(e => e.Id).ToHashSet();
            return recents.Take(5).Concat(frequent).Distinct().Where(available.Contains).Take(12).ToArray();
        }

        var terms = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return source.Select(entry =>
        {
            var label = Fold(entry.Label);
            var all = Fold(entry.Label + " " + entry.Category + " " + entry.Keywords + " " + entry.Id);
            var score = terms.All(all.Contains) ? terms.Sum(t => label == t ? 100 : label.StartsWith(t) ? 50 : label.Contains(t) ? 25 : 5) : -1;
            return (entry, score);
        }).Where(row => row.score >= 0).OrderByDescending(row => row.score).ThenBy(row => row.entry.Category).ThenBy(row => row.entry.Label).Select(row => row.entry.Id).ToArray();
    }
}

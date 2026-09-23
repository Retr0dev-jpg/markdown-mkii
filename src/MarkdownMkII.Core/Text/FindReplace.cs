using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public readonly record struct FindMatch(int Start, int Length);

public sealed class FindReplaceOptions
{
    public bool MatchCase { get; init; }
    public bool WholeWord { get; init; }
    public bool UseRegex { get; init; }
    public bool WrapAround { get; init; } = true;
    public int RangeStart { get; init; }
    public int RangeLength { get; init; } = -1;
}

public static class FindReplace
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public static FindMatch? FindNext(string text, string query, int from, FindReplaceOptions? options = null)
    {
        options ??= new FindReplaceOptions();
        var matches = FindAll(text, query, options);
        foreach (var match in matches)
        {
            if (match.Start >= from)
            {
                return match;
            }
        }

        return options.WrapAround && matches.Count > 0 ? matches[0] : null;
    }

    public static FindMatch? FindPrevious(string text, string query, int from, FindReplaceOptions? options = null)
    {
        options ??= new FindReplaceOptions();
        var matches = FindAll(text, query, options);
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            if (matches[i].Start < from)
            {
                return matches[i];
            }
        }

        return options.WrapAround && matches.Count > 0 ? matches[^1] : null;
    }

    public static IReadOnlyList<FindMatch> FindAll(string text, string query, FindReplaceOptions? options = null)
        => Matches(text, query, options ?? new FindReplaceOptions()).Select(hit => hit.Span).ToList();

    /// <summary>Replaces an exact query match, preserving regex context and capture substitutions.</summary>
    public static EditResult? ReplaceMatch(
        string text,
        string query,
        string replacement,
        FindMatch selection,
        FindReplaceOptions? options = null)
    {
        text ??= string.Empty;
        replacement ??= string.Empty;
        foreach (var hit in Matches(text, query, options ?? new FindReplaceOptions()))
        {
            if (hit.Span != selection)
            {
                continue;
            }

            var value = hit.Replacement(replacement);
            return new EditResult(ReplaceAt(text, selection, value), selection.Start, value.Length);
        }

        return null;
    }

    public static (string Text, int Replacements) ReplaceAll(
        string text,
        string query,
        string replacement,
        FindReplaceOptions? options = null)
    {
        text ??= string.Empty;
        replacement ??= string.Empty;
        var matches = Matches(text, query, options ?? new FindReplaceOptions());
        if (matches.Count == 0)
        {
            return (text, 0);
        }

        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        foreach (var hit in matches)
        {
            builder.Append(text, cursor, hit.Span.Start - cursor);
            builder.Append(hit.Replacement(replacement));
            cursor = hit.Span.Start + hit.Span.Length;
        }

        builder.Append(text, cursor, text.Length - cursor);
        return (builder.ToString(), matches.Count);
    }

    public static string ReplaceAt(string text, FindMatch match, string replacement)
    {
        text ??= string.Empty;
        if (match.Start < 0 || match.Start > text.Length || match.Length < 0 || match.Length > text.Length - match.Start)
        {
            return text;
        }

        return text[..match.Start] + replacement + text[(match.Start + match.Length)..];
    }

    private static List<SearchHit> Matches(string text, string query, FindReplaceOptions options)
    {
        text ??= string.Empty;
        query ??= string.Empty;
        var results = new List<SearchHit>();
        if (query.Length == 0 || text.Length == 0)
        {
            return results;
        }

        var rangeStart = options.RangeLength < 0 ? 0 : Math.Clamp(options.RangeStart, 0, text.Length);
        var rangeEnd = options.RangeLength < 0
            ? text.Length
            : rangeStart + Math.Clamp(options.RangeLength, 0, text.Length - rangeStart);

        bool Include(int start, int length)
            => length > 0 && start >= rangeStart && start + length <= rangeEnd &&
               (!options.WholeWord || IsWholeWord(text, start, length));

        if (options.UseRegex)
        {
            try
            {
                var flags = RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                foreach (Match match in Regex.Matches(text, query, flags, RegexTimeout))
                {
                    if (Include(match.Index, match.Length))
                    {
                        results.Add(new SearchHit(new FindMatch(match.Index, match.Length), match));
                    }
                }
            }
            catch (ArgumentException)
            {
                return [];
            }
            catch (RegexMatchTimeoutException)
            {
                // An incomplete search must never become a partial replacement operation.
                return [];
            }

            return results;
        }

        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var cursor = 0;
        while (cursor <= text.Length - query.Length)
        {
            var index = text.IndexOf(query, cursor, comparison);
            if (index < 0 || index >= rangeEnd)
            {
                break;
            }

            if (Include(index, query.Length))
            {
                results.Add(new SearchHit(new FindMatch(index, query.Length), null));
            }

            cursor = index + query.Length;
        }

        return results;
    }

    private static bool IsWholeWord(string text, int start, int length)
        => (start == 0 || !IsWordChar(text[start - 1])) &&
           (start + length >= text.Length || !IsWordChar(text[start + length]));

    private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch is '_' or '\'';

    private readonly record struct SearchHit(FindMatch Span, Match? RegexMatch)
    {
        public string Replacement(string value)
        {
            if (RegexMatch is null) return value;
            try { return RegexMatch.Result(value); }
            catch (ArgumentException) { return value; }
        }
    }
}

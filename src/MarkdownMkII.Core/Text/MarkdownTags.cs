using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public static partial class MarkdownTags
{
    [GeneratedRegex(@"(?<![&\w])#([A-Za-z][\w\-]{0,40})")]
    private static partial Regex Pattern();

    public static IReadOnlyList<string> Extract(
        string markdown,
        IReadOnlyDictionary<string, string>? frontMatter = null)
    {
        var tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        markdown ??= string.Empty;
        var map = new LineMap(markdown);
        var fenceState = new MarkdownFence();
        for (var line = 0; line < map.LineCount; line++)
        {
            var text = map.LineText(line);
            var trimmed = text.TrimStart();
            if (fenceState.Advance(text))
            {
                continue;
            }

            if (fenceState.IsOpen || IsAtxHeading(trimmed))
            {
                continue;
            }

            foreach (Match match in Pattern().Matches(text))
            {
                var tag = match.Groups[1].Value;
                if (tag.Length > 0)
                {
                    tags.Add(tag);
                }
            }
        }

        if (frontMatter is not null)
        {
            foreach (var key in new[] { "tags", "tag" })
            {
                if (!frontMatter.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                foreach (var piece in raw.Split([',', ' ', '[', ']', '|'], StringSplitOptions.RemoveEmptyEntries))
                {
                    var tag = piece.Trim().Trim('#');
                    if (tag.Length > 0)
                    {
                        tags.Add(tag);
                    }
                }
            }
        }

        return tags.ToList();
    }

    public static string Rewrite(string markdown, string oldTag, string newTag)
    {
        markdown ??= string.Empty;
        oldTag = (oldTag ?? string.Empty).Trim().TrimStart('#');
        newTag = (newTag ?? string.Empty).Trim().TrimStart('#');
        if (oldTag.Length == 0 ||
            newTag.Length == 0 ||
            string.Equals(oldTag, newTag, StringComparison.OrdinalIgnoreCase))
        {
            return markdown;
        }

        if (!FrontMatter.TrySplit(markdown, out var fields, out _) || !FrontMatter.TryLocate(markdown, out _, out _, out var bodyStart))
        {
            return Extract(markdown, null).Contains(oldTag, StringComparer.OrdinalIgnoreCase)
                ? RewriteBody(markdown, oldTag, newTag)
                : markdown;
        }

        if (!Extract(markdown, fields).Contains(oldTag, StringComparer.OrdinalIgnoreCase))
        {
            return markdown;
        }

        var head = markdown[..bodyStart];
        foreach (var key in new[] { "tags", "tag" })
        {
            if (!fields.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var pieces = FrontMatter.List(fields, key)
                .SelectMany(item => item.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Select(piece =>
                {
                    var tag = piece.Trim().Trim('#');
                    return string.Equals(tag, oldTag, StringComparison.OrdinalIgnoreCase) ? newTag : tag;
                })
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            head = FrontMatter.Upsert(head, key, "[" + string.Join(", ", pieces) + "]");
        }

        return head + RewriteBody(markdown[bodyStart..], oldTag, newTag);
    }

    private static string RewriteBody(string markdown, string oldTag, string newTag)
    {
        var map = new LineMap(markdown);
        var fenceState = new MarkdownFence();
        var lines = new List<string>(map.LineCount);
        for (var line = 0; line < map.LineCount; line++)
        {
            var text = map.LineText(line);
            var trimmed = text.TrimStart();
            if (fenceState.Advance(text))
            {
                lines.Add(text);
                continue;
            }

            if (fenceState.IsOpen || IsAtxHeading(trimmed))
            {
                lines.Add(text);
                continue;
            }

            lines.Add(Regex.Replace(
                text,
                @"(?<![&\w])#" + Regex.Escape(oldTag) + @"(?![\w-])",
                "#" + newTag,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        }

        var joined = string.Join('\n', lines);
        if (markdown.EndsWith('\n') && !joined.EndsWith('\n'))
        {
            joined += "\n";
        }

        return joined;
    }

    private static bool IsAtxHeading(string trimmed)
    {
        var i = 0;
        while (i < trimmed.Length && trimmed[i] == '#')
        {
            i++;
        }

        return i is >= 1 and <= 6 && i < trimmed.Length && trimmed[i] == ' ';
    }
}

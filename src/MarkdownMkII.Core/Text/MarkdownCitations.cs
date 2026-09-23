using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public static class MarkdownCitations
{
    public static IReadOnlyList<string> Keys(string markdown)
    {
        markdown ??= string.Empty;
        var keys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (Match match in Regex.Matches(
                         markdown,
                         @"\[@([A-Za-z][\w:.-]*)",
                         RegexOptions.CultureInvariant,
                         TimeSpan.FromMilliseconds(250)))
            {
                var key = match.Groups[1].Value.Trim();
                if (key.Length > 0)
                {
                    keys.Add(key);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
        }

        return keys.ToList();
    }

    public static string Format(IReadOnlyList<string> keys, string heading = "References")
    {
        heading = string.IsNullOrWhiteSpace(heading) ? "References" : heading.Trim();
        var builder = new System.Text.StringBuilder();
        builder.Append("## ").Append(heading).AppendLine().AppendLine();
        if (keys is null || keys.Count == 0)
        {
            return builder.ToString();
        }

        foreach (var key in keys)
        {
            builder.Append("- [@").Append(key).AppendLine("]");
        }

        return builder.ToString();
    }

    public static EditResult Upsert(string markdown, string heading = "References")
    {
        var keys = Keys(markdown);
        var body = keys.Count == 0
            ? string.Empty
            : string.Join('\n', keys.Select(key => "- [@" + key + "]")) + "\n";
        return MarkdownEditing.UpsertHeadingSection(markdown, heading, body);
    }
}

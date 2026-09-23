using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Markdown;

public readonly record struct WikiLinkHit(
    int Start,
    int Length,
    string Target,
    string Label,
    string? Heading,
    bool IsEmbed);

public static partial class WikiLinks
{
    [GeneratedRegex(@"(!?)\[\[([^\]|#\r\n]*)(?:#([^\]|\r\n]+))?(?:\|([^\]\r\n]+))?\]\]")]
    public static partial Regex Pattern();

    public static IEnumerable<WikiLinkHit> Find(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (Match match in Pattern().Matches(text))
        {
            var target = match.Groups[2].Value.Trim();
            var heading = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
            if (string.IsNullOrEmpty(heading))
            {
                heading = null;
            }

            if (target.Length == 0 && heading is null)
            {
                continue;
            }

            var label = match.Groups[4].Success
                ? match.Groups[4].Value.Trim()
                : heading is null
                    ? target
                    : target.Length == 0 ? heading : target + "#" + heading;
            yield return new WikiLinkHit(
                match.Index,
                match.Length,
                target,
                label,
                heading,
                match.Groups[1].Value == "!");
        }
    }

    public static string RewriteTarget(string text, string oldTarget, string newTarget)
    {
        text ??= string.Empty;
        oldTarget = (oldTarget ?? string.Empty).Trim();
        newTarget = (newTarget ?? string.Empty).Trim();
        if (oldTarget.Length == 0 || newTarget.Length == 0)
        {
            return text;
        }

        return Pattern().Replace(text, match =>
        {
            if (!string.Equals(match.Groups[2].Value.Trim(), oldTarget, StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            var heading = match.Groups[3].Success ? "#" + match.Groups[3].Value : string.Empty;
            var label = match.Groups[4].Success ? "|" + match.Groups[4].Value : string.Empty;
            return match.Groups[1].Value + "[[" + newTarget + heading + label + "]]";
        });
    }

    public static string RewriteHeading(string text, string noteTarget, string oldHeading, string newHeading)
    {
        text ??= string.Empty;
        noteTarget = (noteTarget ?? string.Empty).Trim();
        if (noteTarget.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            noteTarget = noteTarget[..^3];
        }

        oldHeading = (oldHeading ?? string.Empty).Trim();
        newHeading = (newHeading ?? string.Empty).Trim();
        if (oldHeading.Length == 0 ||
            newHeading.Length == 0 ||
            HeadingEquals(oldHeading, newHeading))
        {
            return text;
        }

        var next = Pattern().Replace(text, match =>
        {
            var target = match.Groups[2].Value.Trim();
            var heading = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty;
            if (heading.Length == 0 || !HeadingEquals(heading, oldHeading) || !TargetMatches(target, noteTarget))
            {
                return match.Value;
            }

            var bang = match.Groups[1].Value;
            var label = match.Groups[4].Success ? match.Groups[4].Value : string.Empty;
            var inner = target.Length == 0 ? "#" + newHeading : target + "#" + newHeading;
            return label.Length > 0
                ? bang + "[[" + inner + "|" + label + "]]"
                : bang + "[[" + inner + "]]";
        });

        try
        {
            next = Regex.Replace(
                next,
                @"(\]\()([^)#]*)#([^)]+)(\))",
                match =>
                {
                    var file = match.Groups[2].Value.Trim();
                    var fragment = match.Groups[3].Value.Trim();
                    if (!HeadingEquals(fragment, oldHeading) || !MarkdownTargetMatches(file, noteTarget))
                    {
                        return match.Value;
                    }

                    return match.Groups[1].Value + file + "#" + newHeading + match.Groups[4].Value;
                },
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(80));
        }
        catch (RegexMatchTimeoutException)
        {
        }

        return next;
    }

    private static bool HeadingEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(
               MarkdownMkII.Core.Text.MarkdownEditing.HeadingAnchor(left),
               MarkdownMkII.Core.Text.MarkdownEditing.HeadingAnchor(right),
               StringComparison.OrdinalIgnoreCase);

    private static bool TargetMatches(string target, string noteTarget)
    {
        if (noteTarget.Length == 0)
        {
            return target.Length == 0;
        }

        return string.Equals(target, noteTarget, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Path.GetFileNameWithoutExtension(target.Replace('\\', '/')), noteTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MarkdownTargetMatches(string file, string noteTarget)
    {
        if (noteTarget.Length == 0)
        {
            return file.Length == 0;
        }

        var fileName = (file ?? string.Empty).Replace('\\', '/');
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return string.Equals(stem, noteTarget, StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(noteTarget + ".md", StringComparison.OrdinalIgnoreCase);
    }

    public static string RewriteMarkdownFileLinks(string text, string oldFileName, string newFileName)
    {
        text ??= string.Empty;
        oldFileName = (oldFileName ?? string.Empty).Trim();
        newFileName = (newFileName ?? string.Empty).Trim();
        if (oldFileName.Length == 0 || newFileName.Length == 0)
        {
            return text;
        }

        var pattern = new Regex(
            @"(\]\()((?:\./)?)(" + Regex.Escape(oldFileName) + @")(#[^)\s]*)?(\))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return pattern.Replace(text, match =>
            match.Groups[1].Value + match.Groups[2].Value + newFileName + match.Groups[4].Value + match.Groups[5].Value);
    }

    public static WikiLinkHit? AtCaret(string text, int caret)
    {
        foreach (var hit in Find(text))
        {
            if (caret >= hit.Start && caret <= hit.Start + hit.Length)
            {
                return hit;
            }
        }

        return null;
    }

    public static (int Start, int Length, string Url)? MarkdownLinkAt(string text, int caret)
    {
        text ??= string.Empty;
        caret = Math.Clamp(caret, 0, text.Length);
        try
        {
            foreach (Match match in Regex.Matches(
                         text,
                         @"\[[^\]]*\]\(([^)\s]+)\)",
                         RegexOptions.None,
                         TimeSpan.FromMilliseconds(80)))
            {
                if (caret >= match.Index && caret <= match.Index + match.Length)
                {
                    return (match.Index, match.Length, match.Groups[1].Value.Trim());
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
        }

        return null;
    }

    public static string ToMarkdownLinks(string text)
    {
        text ??= string.Empty;
        return Pattern().Replace(text, match =>
        {
            if (match.Groups[1].Value == "!")
            {
                return match.Value;
            }

            var target = match.Groups[2].Value.Trim();
            var heading = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty;
            var label = match.Groups[4].Success ? match.Groups[4].Value.Trim() : target.Length == 0 ? heading : target;
            if (label.Length == 0)
            {
                label = target.Length == 0 ? heading : target;
            }

            var href = target.Length == 0 || target.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? target : target + ".md";
            if (heading.Length > 0)
            {
                href += "#" + heading;
            }

            return "[" + label + "](" + href + ")";
        });
    }

    public static string ToWikilinks(string text)
    {
        text ??= string.Empty;
        try
        {
            return Regex.Replace(
                text,
                @"\[([^\]]+)\]\(([^)\s]+\.md)(#[^)\s]+)?\)",
                match =>
                {
                    var label = match.Groups[1].Value.Trim();
                    var file = match.Groups[2].Value.Trim();
                    var heading = match.Groups[3].Success ? match.Groups[3].Value.Trim().TrimStart('#') : string.Empty;
                    var stem = file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? file[..^3] : file;
                    stem = stem.Replace('\\', '/');
                    if (stem.StartsWith("./", StringComparison.Ordinal))
                    {
                        stem = stem[2..];
                    }
                    var inner = heading.Length > 0 ? stem + "#" + heading : stem;
                    if (string.Equals(label, stem, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(label, inner, StringComparison.OrdinalIgnoreCase))
                    {
                        return "[[" + inner + "]]";
                    }

                    return "[[" + inner + "|" + label + "]]";
                },
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(250));
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }
    }

    public static string Unwrap(string text)
    {
        text ??= string.Empty;
        return Pattern().Replace(text, match =>
        {
            if (match.Groups[1].Value == "!")
            {
                return match.Value;
            }

            var target = match.Groups[2].Value.Trim();
            var heading = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty;
            var label = match.Groups[4].Success ? match.Groups[4].Value.Trim() : string.Empty;
            if (label.Length > 0)
            {
                return label;
            }

            if (heading.Length > 0 && target.Length == 0)
            {
                return heading;
            }

            return heading.Length == 0 ? target : target + "#" + heading;
        });
    }
}

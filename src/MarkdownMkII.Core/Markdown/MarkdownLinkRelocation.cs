using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkdownMkII.Core.Markdown;

/// <summary>Rebases Markdown destinations when their containing note moves.</summary>
public static class MarkdownLinkRelocation
{
    private static readonly MarkdownPipeline Pipeline = CreatePipeline();
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static string Rebase(string text, string oldDocumentPath, string newDocumentPath)
    {
        text ??= string.Empty;
        if (text.Length == 0)
        {
            return text;
        }

        string oldPath;
        string newPath;
        try
        {
            oldPath = Path.GetFullPath(oldDocumentPath);
            newPath = Path.GetFullPath(newDocumentPath);
        }
        catch (ArgumentException) { return text; }
        catch (NotSupportedException) { return text; }
        catch (IOException) { return text; }

        if (string.Equals(oldPath, newPath, PathComparison))
        {
            return text;
        }

        var oldDirectory = Path.GetDirectoryName(oldPath)!;
        var newDirectory = Path.GetDirectoryName(newPath)!;
        var replacements = new SortedDictionary<int, (int Length, string Text)>();
        var document = Markdig.Markdown.Parse(text, Pipeline);
        foreach (var node in document.Descendants())
        {
            switch (node)
            {
                case LinkInline { Reference: null } link when !IsWikiLink(text, link.Span):
                    AddDestination(link.Url, link.UrlSpan);
                    break;
                case LinkReferenceDefinition definition:
                    AddDestination(definition.Url, definition.UrlSpan);
                    break;
            }
        }

        if (replacements.Count == 0)
        {
            return text;
        }

        var result = new StringBuilder(text.Length);
        var cursor = 0;
        foreach (var (start, replacement) in replacements)
        {
            if (start < cursor)
            {
                continue;
            }

            result.Append(text, cursor, start - cursor).Append(replacement.Text);
            cursor = start + replacement.Length;
        }

        return result.Append(text, cursor, text.Length - cursor).ToString();

        void AddDestination(string? url, SourceSpan span)
        {
            if (string.IsNullOrEmpty(url) || span.Start < 0 || span.End < span.Start || span.End >= text.Length)
            {
                return;
            }

            var original = text.Substring(span.Start, span.Length);
            var pointy = original.StartsWith('<') && original.EndsWith('>');
            var raw = pointy ? original[1..^1] : original;
            var rebased = RebaseDestination(url, raw, oldPath, newPath, oldDirectory, newDirectory);
            if (rebased is not null)
            {
                replacements[span.Start] = (span.Length, pointy ? "<" + rebased + ">" : rebased);
            }
        }
    }

    private static string? RebaseDestination(
        string url, string raw, string oldPath, string newPath, string oldDirectory, string newDirectory)
    {
        if (url[0] is '#' or '?' || url.StartsWith("//", StringComparison.Ordinal) ||
            Path.IsPathRooted(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return null;
        }

        var suffixAt = url.IndexOfAny(['#', '?']);
        var destination = suffixAt < 0 ? url : url[..suffixAt];
        if (destination.Length == 0)
        {
            return null;
        }

        try
        {
            var decoded = Uri.UnescapeDataString(destination).Replace('/', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(decoded, oldDirectory);
            if (string.Equals(target, oldPath, PathComparison))
            {
                target = newPath;
            }
            else if (string.Equals(oldDirectory, newDirectory, PathComparison))
            {
                return null;
            }

            var relative = Path.GetRelativePath(newDirectory, target).Replace('\\', '/');
            var rooted = Path.IsPathRooted(relative);
            var encoded = string.Join('/', relative.Split('/').Select((segment, index) =>
                rooted && index == 0 && segment.Length == 2 && char.IsLetter(segment[0]) && segment[1] == ':'
                    ? segment
                    : Uri.EscapeDataString(segment)));
            var rawSuffixAt = raw.IndexOfAny(['#', '?']);
            var suffix = rawSuffixAt >= 0 ? raw[rawSuffixAt..] : suffixAt >= 0 ? url[suffixAt..] : string.Empty;
            return encoded + suffix;
        }
        catch (ArgumentException) { return null; }
        catch (NotSupportedException) { return null; }
        catch (IOException) { return null; }
    }

    private static bool IsWikiLink(string text, SourceSpan span)
    {
        if (span.Start < 0 || span.Start >= text.Length)
        {
            return false;
        }

        var source = text.AsSpan(span.Start);
        return source.StartsWith("[[", StringComparison.Ordinal) || source.StartsWith("![[", StringComparison.Ordinal);
    }

    private static MarkdownPipeline CreatePipeline()
    {
        var builder = new MarkdownPipelineBuilder().UsePreciseSourceLocation().EnableTrackTrivia();
        foreach (var extension in PipelineFactory.Export.Extensions)
        {
            builder.Extensions.Add(extension);
        }

        return builder.Build();
    }
}

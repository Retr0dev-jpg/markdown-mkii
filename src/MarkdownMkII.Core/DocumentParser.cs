using Markdig.Syntax;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core;

public sealed class ParsedDocument
{
    public required string Text { get; init; }

    public required MarkdownDocument Syntax { get; init; }

    public required PreviewDocument Preview { get; init; }

    public required IReadOnlyList<OutlineNode> Outline { get; init; }

    public required DocumentStats Stats { get; init; }

    public required IReadOnlyDictionary<string, string> FrontMatter { get; init; }

    public required LineMap Lines { get; init; }

    public required IReadOnlyList<MarkdownIssue> Diagnostics { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }
}

public static class DocumentParser
{
    public const int LargeFileCharacters = 1_000_000;

    public const int LargeFileLines = 10_000;

    public static ParsedDocument Parse(
        string markdown,
        string? documentPath = null,
        int selectionStart = 0,
        int selectionLength = 0,
        IEnumerable<string>? vaultFolders = null, NotePreviewContext? notes = null)
    {
        markdown ??= string.Empty;
        var folders = vaultFolders as IReadOnlyList<string> ?? vaultFolders?.ToArray();
        var syntax = Markdig.Markdown.Parse(markdown, PipelineFactory.Preview);
        var preview = PreviewBuilder.Build(syntax, markdown, documentPath, folders, notes);
        var lines = new LineMap(markdown);
        var outline = TocExtractor.Extract(syntax, lines.LineCount, lines);
        var stats = WordStats.Compute(markdown, selectionStart, selectionLength);
        return new ParsedDocument
        {
            Text = markdown,
            Syntax = syntax,
            Preview = preview,
            Outline = outline,
            Stats = stats,
            FrontMatter = preview.FrontMatter,
            Lines = lines,
            Diagnostics = MarkdownDiagnostics.Analyze(markdown, documentPath, folders),
            Tags = MarkdownTags.Extract(markdown, preview.FrontMatter)
        };
    }

    public static bool IsLarge(string markdown)
    {
        markdown ??= string.Empty;
        if (markdown.Length >= LargeFileCharacters)
        {
            return true;
        }

        var lines = 1;
        for (var i = 0; i < markdown.Length; i++)
        {
            if (markdown[i] is not ('\r' or '\n')) continue;
            if (markdown[i] == '\r' && i + 1 < markdown.Length && markdown[i + 1] == '\n') i++;
            if (++lines >= LargeFileLines)
            {
                return true;
            }
        }

        return false;
    }

    public static string ToHtml(string markdown, bool disableHtml = false)
    {
        var pipeline = disableHtml ? PipelineFactory.Preview : PipelineFactory.Export;
        return Markdig.Markdown.ToHtml(markdown ?? string.Empty, pipeline);
    }
}

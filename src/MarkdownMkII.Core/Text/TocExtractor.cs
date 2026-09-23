using Markdig.Renderers.Html;
using Markdig.Syntax;
using MarkdownMkII.Core.Markdown;

namespace MarkdownMkII.Core.Text;

public sealed record OutlineNode(int Level, string Title, string Id, int SourceLine, int SourceEndLine);

public static class TocExtractor
{
    public static IReadOnlyList<OutlineNode> Extract(string markdown)
    {
        markdown ??= string.Empty;
        var document = Markdig.Markdown.Parse(markdown, PipelineFactory.Preview);
        var lines = new LineMap(markdown);
        return Extract(document, lines.LineCount, lines);
    }

    public static IReadOnlyList<OutlineNode> Extract(MarkdownDocument document, int lineCount = 0, LineMap? sourceLines = null)
    {
        var nodes = new List<OutlineNode>();
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var title = InlineText(heading.Inline);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var id = heading.TryGetAttributes()?.Id ?? title;
            // Markdig reports a Setext heading's underline as HeadingBlock.Line.
            var sourceLine = sourceLines?.LineOfOffset(heading.Span.Start)
                ?? Math.Max(0, heading.Line - (heading.IsSetext ? 1 : 0));
            nodes.Add(new OutlineNode(heading.Level, title, id, sourceLine, sourceLine));
        }

        AssignSections(nodes, lineCount);
        return nodes;
    }

    public static (int StartLine, int EndLine)? RangeAt(IReadOnlyList<OutlineNode> outline, int line)
    {
        OutlineNode? best = null;
        foreach (var node in outline)
        {
            if (line < node.SourceLine || line > node.SourceEndLine)
            {
                continue;
            }

            if (best is null || node.Level >= best.Level)
            {
                best = node;
            }
        }

        return best is null ? null : (best.SourceLine, best.SourceEndLine);
    }

    public static IReadOnlyList<string> Breadcrumb(IReadOnlyList<OutlineNode> outline, int line)
    {
        if (outline is null || outline.Count == 0)
        {
            return [];
        }

        return outline
            .Where(node => line >= node.SourceLine && line <= node.SourceEndLine)
            .Select(node => node.Title)
            .ToList();
    }

    private static void AssignSections(List<OutlineNode> nodes, int lineCount)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var end = lineCount > 0 ? lineCount - 1 : nodes[i].SourceLine;
            for (var j = i + 1; j < nodes.Count; j++)
            {
                if (nodes[j].Level <= nodes[i].Level)
                {
                    end = Math.Max(nodes[i].SourceLine, nodes[j].SourceLine - 1);
                    break;
                }
            }

            nodes[i] = nodes[i] with { SourceEndLine = end };
        }
    }

    private static string InlineText(Markdig.Syntax.Inlines.ContainerInline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var child in inline)
        {
            Append(child, builder);
        }

        return builder.ToString().Trim();
    }

    private static void Append(Markdig.Syntax.Inlines.Inline inline, System.Text.StringBuilder builder)
    {
        switch (inline)
        {
            case Markdig.Syntax.Inlines.LiteralInline literal:
                builder.Append(literal.Content);
                break;
            case Markdig.Syntax.Inlines.CodeInline code:
                builder.Append(code.Content);
                break;
            case Markdig.Syntax.Inlines.ContainerInline container:
                foreach (var child in container)
                {
                    Append(child, builder);
                }

                break;
        }
    }
}

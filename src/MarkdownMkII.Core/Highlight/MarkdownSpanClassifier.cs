using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownMkII.Core.Markdown;

namespace MarkdownMkII.Core.Highlight;

public enum MarkdownSpanKind
{
    Heading,
    Emphasis,
    Strong,
    Code,
    CodeFence,
    Link,
    Image,
    Quote,
    ListMarker,
    TablePipe,
    Html,
    FrontMatter,
    Strikethrough,
    TaskMarker,
    ThematicBreak,
    Math,
    WikiLink
}

public readonly record struct MarkdownSpan(int Start, int Length, MarkdownSpanKind Kind);

public static class MarkdownSpanClassifier
{
    /// <summary>
    /// Classifica il testo sorgente. Per file enormi si limitano gli span a heading e fence.
    /// </summary>
    public static IReadOnlyList<MarkdownSpan> Classify(string markdown, bool largeFile = false)
    {
        markdown ??= string.Empty;
        if (markdown.Length == 0)
        {
            return [];
        }

        var document = Markdig.Markdown.Parse(markdown, PipelineFactory.Preview);
        return Classify(document, markdown, largeFile);
    }

    public static IReadOnlyList<MarkdownSpan> Classify(MarkdownDocument document, string markdown, bool largeFile = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        markdown ??= string.Empty;
        var spans = new List<MarkdownSpan>();

        foreach (var block in document.Descendants<Block>())
        {
            switch (block)
            {
                case YamlFrontMatterBlock yaml:
                    Add(spans, yaml.Span, MarkdownSpanKind.FrontMatter);
                    break;
                case HeadingBlock heading:
                    Add(spans, heading.Span, MarkdownSpanKind.Heading);
                    break;
                case QuoteBlock quote:
                    if (!largeFile)
                    {
                        Add(spans, quote.Span.Start, 1, MarkdownSpanKind.Quote);
                    }

                    break;
                case ListBlock:
                    if (!largeFile)
                    {
                        Add(spans, block.Span.Start, Math.Min(2, block.Span.Length), MarkdownSpanKind.ListMarker);
                    }

                    break;
                case Markdig.Extensions.Mathematics.MathBlock:
                    Add(spans, block.Span, MarkdownSpanKind.Math);
                    break;
                case FencedCodeBlock fence:
                    Add(spans, fence.Span, MarkdownSpanKind.CodeFence);
                    break;
                case CodeBlock:
                    if (!largeFile)
                    {
                        Add(spans, block.Span, MarkdownSpanKind.Code);
                    }

                    break;
                case HtmlBlock:
                    Add(spans, block.Span, MarkdownSpanKind.Html);
                    break;
                case ThematicBreakBlock:
                    Add(spans, block.Span, MarkdownSpanKind.ThematicBreak);
                    break;
            }

            if (largeFile)
            {
                continue;
            }

            if (block is LeafBlock { Inline: { } inline })
            {
                ClassifyInlines(inline, spans);
            }
        }

        if (!largeFile)
        {
            foreach (var task in document.Descendants<TaskList>())
            {
                Add(spans, task.Span, MarkdownSpanKind.TaskMarker);
            }

            ClassifyTablePipes(markdown, spans);
            ClassifyWikiLinks(markdown, spans);
        }

        spans.Sort((a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }

    private static void ClassifyInlines(ContainerInline root, List<MarkdownSpan> spans)
    {
        foreach (var inline in root.FindDescendants<Inline>())
        {
            switch (inline)
            {
                case EmphasisInline emphasis:
                    var kind = (emphasis.DelimiterChar, emphasis.DelimiterCount) switch
                    {
                        ('~', 2) => MarkdownSpanKind.Strikethrough,
                        ('=', 2) or ('+', 2) => MarkdownSpanKind.Emphasis,
                        ('*', 2) or ('_', 2) => MarkdownSpanKind.Strong,
                        _ => MarkdownSpanKind.Emphasis
                    };
                    Add(spans, emphasis.Span, kind);
                    break;
                case CodeInline:
                    Add(spans, inline.Span, MarkdownSpanKind.Code);
                    break;
                case LinkInline { IsImage: true }:
                    Add(spans, inline.Span, MarkdownSpanKind.Image);
                    break;
                case LinkInline:
                    Add(spans, inline.Span, MarkdownSpanKind.Link);
                    break;
                case AutolinkInline:
                    Add(spans, inline.Span, MarkdownSpanKind.Link);
                    break;
                case HtmlInline:
                    Add(spans, inline.Span, MarkdownSpanKind.Html);
                    break;
                case Markdig.Extensions.Mathematics.MathInline:
                    Add(spans, inline.Span, MarkdownSpanKind.Math);
                    break;
            }
        }
    }

    private static void ClassifyWikiLinks(string markdown, List<MarkdownSpan> spans)
    {
        foreach (var hit in WikiLinks.Find(markdown))
        {
            spans.Add(new MarkdownSpan(hit.Start, hit.Length, MarkdownSpanKind.WikiLink));
        }
    }

    private static void ClassifyTablePipes(string markdown, List<MarkdownSpan> spans)
    {
        for (var i = 0; i < markdown.Length; i++)
        {
            if (markdown[i] == '|' && (i == 0 || markdown[i - 1] == '\n' || markdown[i - 1] == ' '))
            {
                // Heuristic: pipes on lines that look like tables. Cheap and good enough for highlighting.
                var lineStart = markdown.LastIndexOf('\n', Math.Max(0, i - 1)) + 1;
                var lineEnd = markdown.IndexOf('\n', i);
                if (lineEnd < 0)
                {
                    lineEnd = markdown.Length;
                }

                var line = markdown[lineStart..lineEnd];
                if (line.Contains('|', StringComparison.Ordinal) && line.Count(ch => ch == '|') >= 2)
                {
                    if (markdown[i] == '|')
                    {
                        spans.Add(new MarkdownSpan(i, 1, MarkdownSpanKind.TablePipe));
                    }
                }
            }
        }
    }

    private static void Add(List<MarkdownSpan> spans, Markdig.Syntax.SourceSpan span, MarkdownSpanKind kind)
    {
        if (span.Length <= 0 || span.Start < 0)
        {
            return;
        }

        spans.Add(new MarkdownSpan(span.Start, span.Length, kind));
    }

    private static void Add(List<MarkdownSpan> spans, int start, int length, MarkdownSpanKind kind)
    {
        if (start < 0 || length <= 0)
        {
            return;
        }

        spans.Add(new MarkdownSpan(start, length, kind));
    }
}

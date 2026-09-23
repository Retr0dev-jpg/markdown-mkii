using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkdownMkII.Core.Markdown;

/// <summary>
/// Parser inline che riconosce <c>[[target]]</c> e <c>[[target|label]]</c> prima del parser dei link Markdig,
/// così le parentesi non vengono spezzate in riferimenti vuoti.
/// </summary>
public sealed class WikiLinkExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.InlineParsers.Contains<WikiLinkInlineParser>())
        {
            pipeline.InlineParsers.Insert(0, new WikiLinkInlineParser());
        }
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}

public sealed class WikiLinkInlineParser : InlineParser
{
    public WikiLinkInlineParser()
    {
        OpeningCharacters = ['[', '!'];
    }

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var isEmbed = false;
        if (slice.CurrentChar == '!')
        {
            if (slice.PeekChar() != '[')
            {
                return false;
            }

            isEmbed = true;
        }
        else if (slice.CurrentChar != '[' || slice.PeekChar() != '[')
        {
            return false;
        }

        var saved = slice;
        var start = slice.Start;
        if (isEmbed)
        {
            slice.NextChar();
        }

        if (slice.CurrentChar != '[' || slice.PeekChar() != '[')
        {
            slice = saved;
            return false;
        }

        slice.NextChar();
        slice.NextChar();

        var contentStart = slice.Start;
        var pipe = -1;
        while (!slice.IsEmpty)
        {
            var current = slice.CurrentChar;
            if (current is '\n' or '\0')
            {
                slice = saved;
                return false;
            }

            if (current == '|' && pipe < 0)
            {
                pipe = slice.Start;
            }

            if (current == ']' && slice.PeekChar() == ']')
            {
                var contentEnd = slice.Start;
                slice.NextChar();
                slice.NextChar();

                string raw;
                string label;
                if (pipe >= 0)
                {
                    raw = slice.Text.Substring(contentStart, pipe - contentStart).Trim();
                    label = slice.Text.Substring(pipe + 1, contentEnd - pipe - 1).Trim();
                }
                else
                {
                    raw = slice.Text.Substring(contentStart, contentEnd - contentStart).Trim();
                    label = raw;
                }

                if (raw.Length == 0)
                {
                    slice = saved;
                    return false;
                }

                var hash = raw.IndexOf('#');
                var target = hash >= 0 ? raw[..hash].Trim() : raw;
                var heading = hash >= 0 ? raw[(hash + 1)..].Trim() : null;
                if (target.Length == 0 && string.IsNullOrEmpty(heading))
                {
                    slice = saved;
                    return false;
                }

                if (pipe < 0 && heading is { Length: > 0 })
                {
                    label = target.Length == 0 ? heading : target + "#" + heading;
                }

                string relative;
                var media = target.Length > 0 && WikiIndex.IsMediaTarget(target);
                if (target.Length == 0)
                {
                    relative = "#" + heading;
                }
                else
                {
                    relative = media
                        ? target
                        : target.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? target : target + ".md";
                    if (!media && !string.IsNullOrEmpty(heading))
                    {
                        relative += "#" + heading;
                    }
                }

                var sourceStart = processor.GetSourcePosition(start, out var line, out var column);
                var sourceEnd = processor.GetSourcePosition(slice.Start - 1);
                var link = new LinkInline(relative, title: isEmbed ? "embed" : string.Empty)
                {
                    IsImage = isEmbed && media,
                    IsClosed = true,
                    Span = new SourceSpan(sourceStart, sourceEnd),
                    Line = line,
                    Column = column
                };
                link.AppendChild(new LiteralInline(label));
                processor.Inline = link;
                return true;
            }

            slice.NextChar();
        }

        slice = saved;
        return false;
    }
}

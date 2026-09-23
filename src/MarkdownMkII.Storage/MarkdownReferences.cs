using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownMkII.Core.Markdown;

namespace MarkdownMkII.Storage;
/// <summary>Source spans from the Markdown parser exclude code and escaped examples.</summary>
public sealed record MarkdownReference(string Target, string Label, int Start, int Length, int Line, bool Image, bool Wiki, bool Embed)
{
    public string ReplaceWith(string target)
    {
        if (!Wiki)
            return target;
        var label = Label.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]");
        if (Embed && !Image && !target.StartsWith("attachment:", StringComparison.OrdinalIgnoreCase))
            return "![[" + target + "|" + Label + "]]";
        return (Image || Embed ? "!" : "") + "[" + label + "](" + target + ")";
    }

    public static IReadOnlyList<MarkdownReference> Parse(string markdown)
    {
        var document = Markdig.Markdown.Parse(markdown, PipelineFactory.Preview);
        var result = new List<MarkdownReference>();
        foreach (var block in document.Descendants<LeafBlock>())
        {
            if (block.Inline is null)
                continue;
            foreach (var link in block.Inline.Descendants<LinkInline>())
            {
                if (string.IsNullOrEmpty(link.Url))
                    continue;
                var start = link.Span.Start;
                if (start < 0 || link.Span.End >= markdown.Length)
                    continue;
                var source = markdown.AsSpan(start, link.Span.Length);
                var embed = source.StartsWith("![[");
                var wiki = embed || source.StartsWith("[[");
                var target = link.Url;
                var span = link.UrlSpan;
                if (wiki)
                {
                    var inner = source[(embed ? 3 : 2)..^2];
                    var pipe = inner.IndexOf('|');
                    target = (pipe < 0 ? inner : inner[..pipe]).Trim().ToString();
                    span = link.Span;
                }
                else if (span.Start < 0 || span.End >= markdown.Length || span.Length == 0 || !markdown.AsSpan(span.Start, span.Length).SequenceEqual(link.Url.AsSpan()))
                {
                    // URLs with escapes can differ from their source; valid nonempty URL spans are still safe.
                    if (span.Start <= 0 || span.End >= markdown.Length)
                        continue;
                }

                result.Add(new(target, string.Concat(link.Descendants<LiteralInline>().Select(l => l.Content.ToString())), span.Start, span.Length, link.Line, link.IsImage, wiki, embed));
            }
        }

        return result.DistinctBy(r => (r.Start, r.Length)).ToArray();
    }
}

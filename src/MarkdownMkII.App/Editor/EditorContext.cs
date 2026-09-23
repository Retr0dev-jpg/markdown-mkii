using Markdig.Extensions.Tables;
using Markdig.Syntax;
using MarkdownMkII.Core.Markdown;

namespace MarkdownMkII.Editor;

public readonly record struct EditorContext(bool HasSelection, bool IsTable, bool IsList, bool IsHeading, bool IsCode)
{
    public static EditorContext Inspect(string text, int start, int length, MarkdownDocument? syntax = null)
    {
        start = Math.Clamp(start, 0, text.Length);
        length = Math.Clamp(length, 0, text.Length - start);
        var result = new EditorContext(length > 0, false, false, false, false);
        syntax ??= Markdig.Markdown.Parse(text, PipelineFactory.Preview);
        Visit(syntax);
        return result;

        void Visit(ContainerBlock container)
        {
            foreach (var block in container)
            {
                if (start < block.Span.Start || start > block.Span.End + 1 || start + length > block.Span.End + 1)
                    continue;
                result = result with
                {
                    IsTable = result.IsTable || block is Table,
                    IsList = result.IsList || block is ListBlock,
                    IsHeading = result.IsHeading || block is HeadingBlock,
                    IsCode = result.IsCode || block is CodeBlock
                };
                if (block is ContainerBlock nested) Visit(nested);
            }
        }
    }
}

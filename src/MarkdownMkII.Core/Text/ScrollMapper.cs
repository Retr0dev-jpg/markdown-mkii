namespace MarkdownMkII.Core.Text;

using MarkdownMkII.Core.Preview;

/// <summary>
/// Mappa una riga del sorgente al blocco di anteprima più vicino (SourceLine sull'IR).
/// </summary>
public static class ScrollMapper
{
    public static int MapEditorLineToSourceLine(IReadOnlyList<PreviewBlock> blocks, int editorLine)
    {
        PreviewBlock? best = null;
        foreach (var block in Flatten(blocks))
        {
            if (block.SourceLine <= editorLine &&
                (best is null || block.SourceLine >= best.SourceLine))
            {
                best = block;
            }
        }

        return best?.SourceLine ?? 0;
    }

    public static IReadOnlyList<IReadOnlyList<PreviewBlock>> Paginate(
        IReadOnlyList<PreviewBlock> blocks,
        int maxSourceLinesPerPage)
    {
        maxSourceLinesPerPage = Math.Max(8, maxSourceLinesPerPage);
        var pages = new List<IReadOnlyList<PreviewBlock>>();
        var current = new List<PreviewBlock>();
        var used = 0;
        foreach (var block in blocks)
        {
            var lines = Math.Max(1, block.SourceEndLine - block.SourceLine + 1);
            if (current.Count > 0 && used + lines > maxSourceLinesPerPage)
            {
                pages.Add(current);
                current = [];
                used = 0;
            }

            current.Add(block);
            used += lines;
        }

        if (current.Count > 0)
        {
            pages.Add(current);
        }

        return pages;
    }

    public static IReadOnlyList<IReadOnlyList<PreviewBlock>> Slides(IReadOnlyList<PreviewBlock> blocks)
    {
        if (blocks is null || blocks.Count == 0)
        {
            return [];
        }

        var slides = new List<IReadOnlyList<PreviewBlock>>();
        var current = new List<PreviewBlock>();
        foreach (var block in blocks)
        {
            if (block is HeadingBlockIr { Level: <= 2 } &&
                current.Count > 0 &&
                current.Exists(item => item is not FrontMatterIr))
            {
                slides.Add(current);
                current = [];
            }

            current.Add(block);
        }

        if (current.Count > 0)
        {
            slides.Add(current);
        }

        return slides;
    }

    public static IEnumerable<PreviewBlock> Flatten(IReadOnlyList<PreviewBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            foreach (var child in Children(block))
            {
                foreach (var nested in Flatten([child]))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<PreviewBlock> Children(PreviewBlock block) => block switch
    {
        QuoteBlockIr quote => quote.Children,
        ListBlockIr list => list.Items.SelectMany(item => item.Children),
        FigureBlockIr figure => figure.Children,
        DefinitionListIr definitions => definitions.Items.SelectMany(item => item.Definitions),
        CustomContainerIr container => container.Children,
        _ => []
    };
}

using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Preview;

public sealed record PreviewDocument(
    IReadOnlyList<PreviewBlock> Blocks,
    IReadOnlyDictionary<string, string> FrontMatter,
    string? FrontMatterRaw);

public abstract record PreviewBlock(int SourceLine, int SourceEndLine);

public sealed record HeadingBlockIr(
    int Level,
    string Id,
    IReadOnlyList<PreviewInline> Inlines,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record ParagraphBlockIr(
    IReadOnlyList<PreviewInline> Inlines,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record QuoteBlockIr(
    IReadOnlyList<PreviewBlock> Children,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record ListBlockIr(
    bool Ordered,
    int StartNumber,
    IReadOnlyList<ListItemIr> Items,
    int SourceLine,
    int SourceEndLine,
    char Kind = '1',
    char Delimiter = '.') : PreviewBlock(SourceLine, SourceEndLine)
{
    /// <summary>Marker for the item numbered <paramref name="number"/>: 1., a., A., i. or I. with the list delimiter.</summary>
    public string Marker(int number) => Kind switch
    {
        'a' => Alphabetic(number).ToLowerInvariant(),
        'A' => Alphabetic(number),
        'i' => Roman(number).ToLowerInvariant(),
        'I' => Roman(number),
        _ => number.ToString(System.Globalization.CultureInfo.InvariantCulture)
    } + Delimiter;

    public static int ParseStart(string? start, char kind)
    {
        start ??= string.Empty;
        if (int.TryParse(start, out var numeric))
            return numeric;
        if (kind is 'a' or 'A' && start.Length > 0 && char.IsAsciiLetter(start[0]))
            return char.ToUpperInvariant(start[0]) - 'A' + 1;
        if (kind is 'i' or 'I')
        {
            var total = 0;
            var previous = 0;
            foreach (var c in start.ToUpperInvariant().Reverse())
            {
                var value = c switch { 'I' => 1, 'V' => 5, 'X' => 10, 'L' => 50, 'C' => 100, 'D' => 500, 'M' => 1000, _ => 0 };
                total += value < previous ? -value : value;
                previous = Math.Max(previous, value);
            }

            return Math.Max(1, total);
        }

        return int.TryParse(start, out var parsed) ? parsed : 1;
    }

    private static string Alphabetic(int number)
    {
        var result = string.Empty;
        for (number = Math.Max(1, number); number > 0; number = (number - 1) / 26)
            result = (char)('A' + (number - 1) % 26) + result;
        return result;
    }

    private static string Roman(int number)
    {
        number = Math.Clamp(number, 1, 3999);
        var builder = new System.Text.StringBuilder();
        foreach (var (value, symbol) in new[] { (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
            while (number >= value) { builder.Append(symbol); number -= value; }
        return builder.ToString();
    }
}

public sealed record ListItemIr(bool? Checked, IReadOnlyList<PreviewBlock> Children, int SourceLine);

public sealed record CodeBlockIr(
    string? Language,
    string Code,
    IReadOnlyList<CodeToken> Tokens,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record TableBlockIr(
    TableGridIr Grid,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record TableGridIr(
    IReadOnlyList<TableAlignment> Alignments,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<PreviewInline>>> Header,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<PreviewInline>>> Rows,
    bool Truncated,
    int TotalRows,
    int TotalColumns);

public enum TableAlignment
{
    Left,
    Center,
    Right
}

public sealed record ThematicBreakIr(int SourceLine, int SourceEndLine)
    : PreviewBlock(SourceLine, SourceEndLine);

public sealed record HtmlStubIr(string Kind, string Preview, int SourceLine, int SourceEndLine)
    : PreviewBlock(SourceLine, SourceEndLine);

public sealed record FrontMatterIr(
    IReadOnlyDictionary<string, string> Fields,
    string Raw,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record FigureBlockIr(
    IReadOnlyList<PreviewBlock> Children,
    IReadOnlyList<PreviewInline>? Caption,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record MathBlockIr(string Expression, int SourceLine, int SourceEndLine)
    : PreviewBlock(SourceLine, SourceEndLine);

public sealed record MermaidBlockIr(MermaidDiagram Diagram, int SourceLine, int SourceEndLine)
    : PreviewBlock(SourceLine, SourceEndLine);

public sealed record DefinitionListIr(
    IReadOnlyList<DefinitionItemIr> Items,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record DefinitionItemIr(
    IReadOnlyList<PreviewInline> Term,
    IReadOnlyList<PreviewBlock> Definitions);

public sealed record CustomContainerIr(
    string? Info,
    IReadOnlyList<PreviewBlock> Children,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public sealed record FootnoteBlockIr(
    string Label,
    IReadOnlyList<PreviewBlock> Children,
    int SourceLine,
    int SourceEndLine) : PreviewBlock(SourceLine, SourceEndLine);

public abstract record PreviewInline;

public sealed record TextInline(string Text) : PreviewInline;

public sealed record StrongInline(IReadOnlyList<PreviewInline> Children) : PreviewInline;

public sealed record EmphasisInline(IReadOnlyList<PreviewInline> Children) : PreviewInline;

public sealed record StrikethroughInline(IReadOnlyList<PreviewInline> Children) : PreviewInline;

public sealed record MarkedInline(IReadOnlyList<PreviewInline> Children) : PreviewInline;

public sealed record SuperscriptInline(IReadOnlyList<PreviewInline> Children) : PreviewInline;

public sealed record SubscriptInline(IReadOnlyList<PreviewInline> Children) : PreviewInline;

public sealed record InsertedInline(IReadOnlyList<PreviewInline> Children) : PreviewInline;

public sealed record CodeInline(string Code) : PreviewInline;

public sealed record LinkInline(
    string Url,
    string? Title,
    bool IsImage,
    IReadOnlyList<PreviewInline> Children) : PreviewInline;

public sealed record LineBreakInline(bool IsHard) : PreviewInline;

public sealed record MathInline(string Expression) : PreviewInline;

public sealed record HtmlInlineStub(string Tag) : PreviewInline;

public sealed record FootnoteRefInline(string Label) : PreviewInline;

public sealed record WikiEmbedInline(
    string Target,
    string? Heading,
    string Url,
    IReadOnlyList<PreviewBlock> Children,
    bool Missing) : PreviewInline;

public readonly record struct CodeToken(int Start, int Length, CodeTokenKind Kind);

public enum CodeTokenKind
{
    Plain,
    Keyword,
    String,
    Comment,
    Number,
    TypeName,
    Punctuation
}

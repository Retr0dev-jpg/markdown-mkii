using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.DefinitionLists;
using Markdig.Extensions.Figures;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownMkII.Core.Highlight;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;
using MdEmphasis = Markdig.Syntax.Inlines.EmphasisInline;
using MdLink = Markdig.Syntax.Inlines.LinkInline;
using MdCode = Markdig.Syntax.Inlines.CodeInline;
using MdLineBreak = Markdig.Syntax.Inlines.LineBreakInline;
using MdMathInline = Markdig.Extensions.Mathematics.MathInline;

namespace MarkdownMkII.Core.Preview;

public static class PreviewBuilder
{
    /// <summary>Relative media in an archive note: <c>attachment-name://{noteId}/{fileName}</c>, resolved by attachment name.</summary>
    public const string AttachmentNameScheme = "attachment-name://";
    public const int MaxTableRows = 50;
    public const int MaxTableColumns = 20;

    public static PreviewDocument Build(string markdown, string? documentPath = null, IEnumerable<string>? vaultFolders = null, NotePreviewContext? notes = null)
    {
        markdown ??= string.Empty;
        var document = Markdig.Markdown.Parse(markdown, PipelineFactory.Preview);
        return Build(document, markdown, documentPath, vaultFolders, notes);
    }

    public static PreviewDocument Build(MarkdownDocument document, string markdown, string? documentPath = null, IEnumerable<string>? vaultFolders = null, NotePreviewContext? notes = null)
    {
        var ctx = BuildContext.Create(markdown, documentPath, vaultFolders, notes);
        var blocks = new List<PreviewBlock>();
        IReadOnlyDictionary<string, string> frontMatter = new Dictionary<string, string>();
        string? frontMatterRaw = null;

        foreach (var block in document)
        {
            if (block is YamlFrontMatterBlock yaml)
            {
                frontMatterRaw = yaml.Lines.ToString();
                FrontMatter.TryParse(frontMatterRaw, out frontMatter);
                blocks.Add(new FrontMatterIr(frontMatter, frontMatterRaw ?? string.Empty, Line(block), EndLine(block, ctx)));
                continue;
            }

            var converted = ConvertBlock(block, ctx);
            if (converted is not null)
            {
                blocks.Add(converted);
            }
        }

        return new PreviewDocument(blocks, frontMatter, frontMatterRaw);
    }

    private readonly record struct BuildContext(
        LineMap Lines,
        string? DocumentPath,
        IReadOnlyList<string>? Folders,
        Dictionary<string, string> Snapshots,
        IReadOnlySet<string> Expanding,
        int Depth, NotePreviewContext? Notes = null)
    {
        public const int MaxEmbedDepth = 4;

        public static BuildContext Create(string text, string? documentPath, IEnumerable<string>? folders, NotePreviewContext? notes = null)
        {
            var snapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(documentPath))
            {
                documentPath = DocumentKey(documentPath);
                snapshots[documentPath] = text;
            }

            return new BuildContext(
                new LineMap(text),
                documentPath,
                folders as IReadOnlyList<string> ?? folders?.ToList(),
                snapshots,
                new HashSet<string>(string.IsNullOrEmpty(documentPath) ? [] : [documentPath], StringComparer.OrdinalIgnoreCase),
                0, notes);
        }
    }

    private static PreviewBlock? ConvertBlock(Block block, BuildContext ctx)
    {
        return block switch
        {
            HeadingBlock heading => new HeadingBlockIr(
                heading.Level,
                heading.TryGetAttributes()?.Id ?? Slug(heading),
                ConvertInlines(heading.Inline, ctx),
                ctx.Lines.LineOfOffset(heading.Span.Start),
                EndLine(heading, ctx)),
            ParagraphBlock paragraph => new ParagraphBlockIr(
                ConvertInlines(paragraph.Inline, ctx),
                Line(paragraph),
                EndLine(paragraph, ctx)),
            QuoteBlock quote => new QuoteBlockIr(
                ConvertChildren(quote, ctx),
                Line(quote),
                EndLine(quote, ctx)),
            ListBlock list => ConvertList(list, ctx),
            MathBlock math => new MathBlockIr(math.Lines.ToString().Trim(), Line(math), EndLine(math, ctx)),
            FencedCodeBlock fence => ConvertFence(fence.Info, fence.Lines.ToString(), Line(fence), EndLine(fence, ctx)),
            CodeBlock code => ConvertFence(null, code.Lines.ToString(), Line(code), EndLine(code, ctx)),
            Table table => ConvertTable(table, ctx),
            ThematicBreakBlock rule => new ThematicBreakIr(Line(rule), EndLine(rule, ctx)),
            HtmlBlock html => new HtmlStubIr("html", Truncate(html.Lines.ToString(), 200), Line(html), EndLine(html, ctx)),
            Figure figure => new FigureBlockIr(
                ConvertChildren(figure, ctx),
                figure.OfType<FigureCaption>().Select(c => ConvertInlines(c.Inline, ctx)).FirstOrDefault(),
                Line(figure),
                EndLine(figure, ctx)),
            DefinitionList definitions => ConvertDefinitions(definitions, ctx),
            CustomContainer container => new CustomContainerIr(
                container.Info,
                ConvertChildren(container, ctx),
                Line(container),
                EndLine(container, ctx)),
            FootnoteGroup footnotes => ConvertFootnotes(footnotes, ctx),
            LinkReferenceDefinitionGroup => null,
            _ when block is ContainerBlock container => new QuoteBlockIr(
                ConvertChildren(container, ctx),
                Line(block),
                EndLine(block, ctx)),
            _ => new HtmlStubIr(block.GetType().Name, string.Empty, Line(block), EndLine(block, ctx))
        };
    }

    private static ListBlockIr ConvertList(ListBlock list, BuildContext ctx)
    {
        var items = new List<ListItemIr>(list.Count);
        var kind = list.IsOrdered && list.BulletType is 'a' or 'A' or 'i' or 'I' ? list.BulletType : '1';
        var start = list.IsOrdered ? ListBlockIr.ParseStart(list.OrderedStart, kind) : 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            bool? check = null;
            // Only the item's own first paragraph can carry its checkbox, not a nested list's paragraph.
            var paragraph = item.Count > 0 ? item[0] as ParagraphBlock : null;
            var task = paragraph?.Inline?.FindDescendants<TaskList>().FirstOrDefault();
            if (task is not null)
            {
                check = task.Checked;
            }

            items.Add(new ListItemIr(check, ConvertChildren(item, ctx), Line(item)));
        }

        return new ListBlockIr(list.IsOrdered, start, items, Line(list), EndLine(list, ctx), kind, list.OrderedDelimiter == ')' ? ')' : '.');
    }

    private static QuoteBlockIr ConvertFootnotes(FootnoteGroup group, BuildContext ctx)
    {
        var children = new List<PreviewBlock>();
        foreach (var footnote in group.OfType<Footnote>())
        {
            var label = footnote.Label?.TrimStart('^') ?? footnote.Order.ToString();
            children.Add(new FootnoteBlockIr(
                label,
                ConvertChildren(footnote, ctx),
                Line(footnote),
                EndLine(footnote, ctx)));
        }

        return new QuoteBlockIr(children, Line(group), EndLine(group, ctx));
    }

    private static DefinitionListIr ConvertDefinitions(DefinitionList list, BuildContext ctx)
    {
        // Markdig nests the terms inside each DefinitionItem; several terms share its definitions.
        var items = new List<DefinitionItemIr>();
        foreach (var item in list.OfType<DefinitionItem>())
        {
            var terms = new List<PreviewInline>();
            var definitions = new List<PreviewBlock>();
            foreach (var child in item)
            {
                if (child is DefinitionTerm term)
                {
                    if (terms.Count > 0) terms.Add(new LineBreakInline(true));
                    terms.AddRange(ConvertInlines(term.Inline, ctx));
                }
                else if (ConvertBlock(child, ctx) is { } block)
                {
                    definitions.Add(block);
                }
            }

            items.Add(new DefinitionItemIr(terms, definitions));
        }

        return new DefinitionListIr(items, Line(list), EndLine(list, ctx));
    }

    private static TableBlockIr ConvertTable(Table table, BuildContext ctx)
    {
        var alignments = table.ColumnDefinitions
            .Select(d => d.Alignment switch
            {
                TableColumnAlign.Center => TableAlignment.Center,
                TableColumnAlign.Right => TableAlignment.Right,
                _ => TableAlignment.Left
            })
            .ToList();

        var header = new List<IReadOnlyList<IReadOnlyList<PreviewInline>>>();
        var rows = new List<IReadOnlyList<IReadOnlyList<PreviewInline>>>();
        var totalRows = 0;
        var bodyRows = 0;
        var totalColumns = alignments.Count;

        foreach (var row in table.OfType<TableRow>())
        {
            totalRows++;
            var cells = row.OfType<TableCell>()
                .Select(cell => (IReadOnlyList<PreviewInline>)cell.Descendants<ParagraphBlock>()
                    .SelectMany((paragraph, index) => (index == 0 ? Array.Empty<PreviewInline>() : [new LineBreakInline(true)]).Concat(ConvertInlines(paragraph.Inline, ctx)))
                    .ToList())
                .Take(MaxTableColumns)
                .ToList();
            totalColumns = Math.Max(totalColumns, row.OfType<TableCell>().Count());

            if (row.IsHeader && header.Count == 0)
            {
                header.Add(cells);
                continue;
            }

            bodyRows++;
            if (rows.Count < MaxTableRows)
            {
                rows.Add(cells);
            }
        }

        var truncated = bodyRows > MaxTableRows || totalColumns > MaxTableColumns;
        return new TableBlockIr(
            new TableGridIr(alignments, header, rows, truncated, totalRows, totalColumns),
            Line(table),
            EndLine(table, ctx));
    }

    private static PreviewBlock ConvertFence(string? language, string code, int start, int end)
    {
        code = code.TrimEnd('\r', '\n');
        if (language is not null &&
            language.Equals("mermaid", StringComparison.OrdinalIgnoreCase) &&
            MermaidParser.TryParse(code, out var diagram))
        {
            return new MermaidBlockIr(diagram, start, end);
        }

        if (language is not null &&
            (language.Equals("plantuml", StringComparison.OrdinalIgnoreCase) ||
             language.Equals("uml", StringComparison.OrdinalIgnoreCase)) &&
            PlantUmlParser.TryParse(code, out var uml))
        {
            return new MermaidBlockIr(uml, start, end);
        }

        if (code.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase) &&
            PlantUmlParser.TryParse(code, out var startUml))
        {
            return new MermaidBlockIr(startUml, start, end);
        }

        if (language is not null &&
            (language.Equals("dot", StringComparison.OrdinalIgnoreCase) ||
             language.Equals("graphviz", StringComparison.OrdinalIgnoreCase)) &&
            DotParser.TryParse(code, out var dot))
        {
            return new MermaidBlockIr(dot, start, end);
        }

        var tokens = CodeTokenizer.Tokenize(code, language);
        return new CodeBlockIr(language, code, tokens, start, end);
    }

    private static List<PreviewBlock> ConvertChildren(ContainerBlock container, BuildContext ctx)
    {
        var list = new List<PreviewBlock>();
        foreach (var child in container)
        {
            if (child is FigureCaption)
            {
                continue;
            }

            var converted = ConvertBlock(child, ctx);
            if (converted is not null)
            {
                list.Add(converted);
            }
        }

        return list;
    }

    private static IReadOnlyList<PreviewInline> ConvertInlines(ContainerInline? inline, BuildContext ctx)
    {
        if (inline is null)
        {
            return [];
        }

        var list = new List<PreviewInline>();
        foreach (var child in inline)
        {
            list.AddRange(ConvertInline(child, ctx));
        }

        return list;
    }

    private static IEnumerable<PreviewInline> ConvertInline(Inline inline, BuildContext ctx)
    {
        switch (inline)
        {
            case LiteralInline literal:
                foreach (var piece in ExpandWikilinks(literal.Content.ToString(), ctx))
                {
                    yield return piece;
                }

                yield break;
            case MdEmphasis emphasis:
                yield return WrapEmphasis(emphasis, ConvertInlines(emphasis, ctx));
                yield break;
            case MdCode code:
                yield return new CodeInline(code.Content);
                yield break;
            case MdLink link:
                if (string.Equals(link.Title, "embed", StringComparison.Ordinal))
                {
                    var (path, heading) = LinkResolver.SplitFragment(link.Url ?? string.Empty);
                    if (WikiIndex.IsMediaTarget(path))
                    {
                        var media = WikiIndex.Resolve(path, ctx.DocumentPath, ctx.Folders) ?? ResolveMediaUrl(link.Url, ctx);
                        yield return new LinkInline(media, null, WikiIndex.IsImageTarget(path), ConvertInlines(link, ctx));
                        yield break;
                    }
                    yield return BuildEmbed(
                        DocumentStore.IsMarkdown(path) ? path[..^Path.GetExtension(path).Length] : path,
                        heading,
                        ResolveUrl(link.Url, ctx),
                        ctx);
                    yield break;
                }

                var url = link.IsImage ? ResolveMediaUrl(link.Url, ctx) : ResolveUrl(link.Url, ctx);
                yield return new LinkInline(url, link.Title, link.IsImage, ConvertInlines(link, ctx));
                yield break;
            case MdLineBreak br:
                yield return new LineBreakInline(br.IsHard);
                yield break;
            case HtmlEntityInline entity:
                yield return new TextInline(entity.Transcoded.ToString());
                yield break;
            case HtmlInline html:
                yield return new HtmlInlineStub(html.Tag);
                yield break;
            case MdMathInline math:
                yield return new MathInline(math.Content.ToString());
                yield break;
            case AutolinkInline auto:
                yield return new LinkInline(auto.IsEmail ? "mailto:" + auto.Url : auto.Url, null, false, [new TextInline(auto.Url)]);
                yield break;
            case FootnoteLink { IsBackLink: false } footnote:
                yield return new FootnoteRefInline(footnote.Footnote.Label?.TrimStart('^') ?? footnote.Footnote.Order.ToString());
                yield break;
            case FootnoteLink:
                yield break;
            case TaskList:
                yield break;
            case ContainerInline container:
                foreach (var child in ConvertInlines(container, ctx))
                {
                    yield return child;
                }

                yield break;
            default:
                if (inline is LeafInline leaf)
                {
                    yield return new TextInline(leaf.ToString() ?? string.Empty);
                }

                yield break;
        }
    }

    private static IEnumerable<PreviewInline> ExpandWikilinks(string text, BuildContext ctx)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var hits = WikiLinks.Find(text).ToList();
        if (hits.Count == 0)
        {
            yield return new TextInline(text);
            yield break;
        }

        var cursor = 0;
        foreach (var hit in hits)
        {
            if (hit.Start > cursor)
            {
                yield return new TextInline(text[cursor..hit.Start]);
            }

            var relative = WikiIndex.IsMediaTarget(hit.Target)
                ? hit.Target
                : hit.Target.Length == 0
                    ? (hit.Heading is { Length: > 0 } ? "#" + hit.Heading : "#")
                    : hit.Target.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? hit.Target : hit.Target + ".md";
            if (hit.Heading is { Length: > 0 } &&
                hit.Target.Length > 0 &&
                !WikiIndex.IsMediaTarget(hit.Target))
            {
                relative += "#" + hit.Heading;
            }

            var url = ResolveUrl(relative, ctx);
            if (hit.Target.Length == 0)
            {
                if (hit.IsEmbed)
                {
                    yield return BuildEmbed(string.Empty, hit.Heading, url, ctx);
                }
                else
                {
                    yield return new LinkInline(relative, null, false, [new TextInline(hit.Label)]);
                }
            }
            else if (WikiIndex.IsMediaTarget(hit.Target))
            {
                var media = WikiIndex.Resolve(hit.Target, ctx.DocumentPath, ctx.Folders) ?? ResolveMediaUrl(hit.Target, ctx);
                yield return new LinkInline(media, hit.Label, hit.IsEmbed && WikiIndex.IsImageTarget(hit.Target), [new TextInline(hit.Label)]);
            }
            else if (hit.IsEmbed)
            {
                yield return BuildEmbed(hit.Target, hit.Heading, url, ctx);
            }
            else
            {
                yield return new LinkInline(url, null, false, [new TextInline(hit.Label)]);
            }
            cursor = hit.Start + hit.Length;
        }

        if (cursor < text.Length)
        {
            yield return new TextInline(text[cursor..]);
        }
    }

    private static PreviewInline WrapEmphasis(MdEmphasis emphasis, IReadOnlyList<PreviewInline> children)
    {
        return (emphasis.DelimiterChar, emphasis.DelimiterCount) switch
        {
            ('*', 2) or ('_', 2) => new StrongInline(children),
            ('*', 1) or ('_', 1) => new EmphasisInline(children),
            ('~', 2) => new StrikethroughInline(children),
            ('~', 1) => new SubscriptInline(children),
            ('^', _) => new SuperscriptInline(children),
            ('+', 2) => new InsertedInline(children),
            ('=', 2) => new MarkedInline(children),
            _ => emphasis.DelimiterCount >= 2 ? new StrongInline(children) : new EmphasisInline(children)
        };
    }

    private static WikiEmbedInline BuildEmbed(string target, string? heading, string url, BuildContext ctx)
    {
        if (ctx.Notes is not null)
        {
            var note = ctx.Notes.Resolve(target.Length == 0 ? ctx.Notes.CurrentId : target);
            if (note is null || ctx.Depth >= BuildContext.MaxEmbedDepth || ctx.Expanding.Contains(note.Id) ||
                !string.IsNullOrEmpty(heading) && !WikiIndex.ContainsHeading(note.Markdown, heading))
                return new WikiEmbedInline(target, heading, url, [], true);
            var body = string.IsNullOrEmpty(heading) ? note.Markdown : WikiIndex.ExtractHeadingSection(note.Markdown, heading);
            var noteContext = new BuildContext(new LineMap(body), null, null, ctx.Snapshots,
                new HashSet<string>(ctx.Expanding) { note.Id }, ctx.Depth + 1, ctx.Notes);
            var ast = Markdig.Markdown.Parse(body, PipelineFactory.Preview);
            var noteBlocks = ast.Where(b => b is not YamlFrontMatterBlock).Select(b => ConvertBlock(b, noteContext)).OfType<PreviewBlock>().ToArray();
            return new WikiEmbedInline(note.Title, heading, "note://" + note.Id, noteBlocks, false);
        }
        target = (target ?? string.Empty).Trim();
        var resolved = target.Length == 0
            ? ctx.DocumentPath
            : WikiIndex.Resolve(target, ctx.DocumentPath, ctx.Folders);
        if (string.IsNullOrEmpty(resolved))
        {
            return new WikiEmbedInline(target, heading, url, [], true);
        }

        resolved = DocumentKey(resolved);
        if (ctx.Depth >= BuildContext.MaxEmbedDepth ||
            (ctx.Expanding.Contains(resolved) && string.IsNullOrEmpty(heading)))
        {
            return new WikiEmbedInline(target, heading, resolved, [], true);
        }

        string markdown;
        try
        {
            if (!ctx.Snapshots.TryGetValue(resolved, out markdown!))
            {
                var info = new FileInfo(resolved);
                if (!info.Exists || info.Length > 200_000)
                    return new WikiEmbedInline(target, heading, resolved, [], true);
                markdown = File.ReadAllText(resolved);
                ctx.Snapshots[resolved] = markdown;
            }
        }
        catch (IOException)
        {
            return new WikiEmbedInline(target, heading, url, [], true);
        }
        catch (UnauthorizedAccessException)
        {
            return new WikiEmbedInline(target, heading, url, [], true);
        }

        if (!string.IsNullOrEmpty(heading))
        {
            if (!WikiIndex.ContainsHeading(markdown, heading))
                return new WikiEmbedInline(target, heading, resolved, [], true);
            markdown = WikiIndex.ExtractHeadingSection(markdown, heading);
        }

        var expanding = new HashSet<string>(ctx.Expanding, StringComparer.OrdinalIgnoreCase) { resolved };
        // Source maps describe the extracted fragment; shared snapshots always retain full documents.
        var nested = new BuildContext(new LineMap(markdown), resolved, ctx.Folders, ctx.Snapshots, expanding, ctx.Depth + 1);
        var document = Markdig.Markdown.Parse(markdown, PipelineFactory.Preview);
        var blocks = new List<PreviewBlock>();
        foreach (var block in document)
        {
            if (block is YamlFrontMatterBlock)
            {
                continue;
            }

            var converted = ConvertBlock(block, nested);
            if (converted is not null)
            {
                blocks.Add(converted);
            }
        }

        return new WikiEmbedInline(target, heading, resolved, blocks, false);
    }

    private static string DocumentKey(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return path;
        }
        catch (NotSupportedException)
        {
            return path;
        }
    }

    // Archive notes have no folder: a relative image can only name an attachment, never another note.
    private static string ResolveMediaUrl(string? url, BuildContext ctx)
    {
        if (ctx.Notes is null) return ResolveUrl(url, ctx);
        url ??= string.Empty;
        if (url.Length == 0 || Uri.TryCreate(url, UriKind.Absolute, out _)) return url;
        var (path, _) = LinkResolver.SplitFragment(url);
        var name = Path.GetFileName(Uri.UnescapeDataString(path).Replace('\\', '/'));
        return name.Length == 0 ? url : AttachmentNameScheme + Uri.EscapeDataString(ctx.Notes.CurrentId) + "/" + Uri.EscapeDataString(name);
    }

    private static string ResolveUrl(string? url, BuildContext ctx)
    {
        if (ctx.Notes is not null)
        {
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith('#') || Uri.TryCreate(url, UriKind.Absolute, out _)) return url ?? string.Empty;
            var (target, fragment) = LinkResolver.SplitFragment(url);
            if (target.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) target = target[..^3];
            return "wiki://" + Uri.EscapeDataString(target) + (fragment is null ? "" : "#" + fragment);
        }

        url ??= string.Empty;
        if (url.Length == 0 || url.StartsWith('#') || IsHttpOrMail(url))
        {
            return url;
        }

        var (path, heading) = LinkResolver.SplitFragment(url);
        var query = path.IndexOf('?');
        path = Uri.UnescapeDataString(query < 0 ? path : path[..query]);
        if (DocumentStore.IsMarkdown(path) || !Path.HasExtension(path))
        {
            var target = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? path[..^3] : path;
            var resolved = WikiIndex.ResolveOrFallback(target.Replace('\\', '/'), ctx.DocumentPath, ctx.Folders);
            return LocalUrl(resolved, heading);
        }

        if (string.IsNullOrWhiteSpace(ctx.DocumentPath))
        {
            return heading is null ? path : path + "#" + heading;
        }

        try
        {
            var root = Path.GetDirectoryName(ctx.DocumentPath);
            if (string.IsNullOrEmpty(root))
            {
                return url;
            }

            var combined = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            return LocalUrl(combined, heading);
        }
        catch (Exception)
        {
            return url;
        }
    }

    private static string LocalUrl(string path, string? heading)
    {
        // A literal hash in a filename must not become an anchor in the rendered URL.
        var url = path.Contains('#') && Path.IsPathFullyQualified(path) ? new Uri(path).AbsoluteUri : path;
        return heading is null ? url : url + "#" + heading;
    }

    private static bool IsHttpOrMail(string url)
        => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
           url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
           url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
           url.StartsWith("file:", StringComparison.OrdinalIgnoreCase);

    private static string Slug(HeadingBlock heading)
    {
        var text = heading.Inline?.FirstChild is LiteralInline literal
            ? literal.Content.ToString()
            : heading.ToString() ?? "heading";
        var chars = text.Trim().ToLowerInvariant().Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : ch is ' ' or '-' ? '-' : '\0').Where(ch => ch != '\0');
        return new string(chars.ToArray()).Trim('-');
    }

    private static int Line(MarkdownObject obj) => obj.Line;

    private static int EndLine(Block block, BuildContext ctx)
    {
        var end = block.Span.End;
        foreach (var child in block.Descendants<Block>()) end = Math.Max(end, child.Span.End);
        return Math.Max(block.Line, ctx.Lines.LineOfOffset(end));
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";
}

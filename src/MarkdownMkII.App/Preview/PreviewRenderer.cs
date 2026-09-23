using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Path = System.IO.Path;

namespace MarkdownMkII.Preview;

public sealed class PreviewRenderer
{
    public const int MaxTableRows = PreviewBuilder.MaxTableRows;
    private double zoom = 1;
    private string? embeddedDocumentPath;

    public UIElement Render(PreviewDocument document, EditorViewModel? editor, double zoomLevel = 1)
    {
        zoom = Math.Clamp(zoomLevel, 0.7, 2);
        var panel = new StackPanel { Spacing = 12 * zoom };
        foreach (var block in document.Blocks)
        {
            panel.Children.Add(RenderBlock(block, editor));
        }

        if (panel.Children.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = Strings.T("PreviewEmpty"),
                Opacity = 0.6,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
            });
        }

        return panel;
    }

    public IReadOnlyList<UIElement> RenderPages(PreviewDocument document, EditorViewModel? editor, double zoomLevel = 1)
    {
        var slices = ScrollMapper.Paginate(document.Blocks, 72);
        if (slices.Count == 0)
        {
            return [Render(document, editor, zoomLevel)];
        }

        return slices
            .Select(blocks => Render(
                new PreviewDocument(blocks, document.FrontMatter, document.FrontMatterRaw),
                editor,
                zoomLevel))
            .ToList();
    }

    private UIElement RenderBlock(PreviewBlock block, EditorViewModel? editor)
    {
        UIElement element = block switch
        {
            FrontMatterIr front => RenderFrontMatter(front, editor),
            HeadingBlockIr heading => RenderHeading(heading, editor),
            ParagraphBlockIr paragraph => RenderParagraph(paragraph, editor),
            QuoteBlockIr quote => RenderQuote(quote, editor),
            ListBlockIr list => RenderList(list, editor),
            CodeBlockIr code => RenderCode(code),
            TableBlockIr table => RenderTable(table, editor),
            ThematicBreakIr => new Rectangle
            {
                Height = 1,
                Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                Margin = new Thickness(0, 8, 0, 8)
            },
            HtmlStubIr html => RenderStub(html.Kind, html.Preview),
            FigureBlockIr figure => RenderFigure(figure, editor),
            MathBlockIr math => RenderStub("math", MathText.ToDisplay(math.Expression)),
            MermaidBlockIr mermaid => RenderMermaid(mermaid),
            DefinitionListIr definitions => RenderDefinitions(definitions, editor),
            CustomContainerIr container => RenderContainer(container, editor),
            FootnoteBlockIr note => RenderFootnote(note, editor),
            _ => RenderStub(block.GetType().Name, string.Empty)
        };

        if (element is FrameworkElement framework)
        {
            framework.Tag = block.SourceLine;
            var line = block.SourceLine;
            if (editor is not null && embeddedDocumentPath is null && block is not ListBlockIr)
            {
                framework.Tapped += (_, args) =>
                {
                    if (args.OriginalSource is CheckBox or Image or HyperlinkButton)
                    {
                        return;
                    }

                    editor.GoToSourceLine(line);
                };
            }
        }

        return element;
    }

    private UIElement RenderFrontMatter(FrontMatterIr front, EditorViewModel? editor)
    {
        var expander = new Expander { Header = Strings.T("FrontMatter"), IsExpanded = false };
        var table = new StackPanel { Spacing = 4 };
        foreach (var pair in front.Fields)
        {
            var key = pair.Key;
            var row = new HyperlinkButton
            {
                Content = pair.Key + ": " + pair.Value,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            row.Click += async (_, _) =>
            {
                if (editor is not null)
                {
                    await editor.EditFrontMatterAsync(key);
                }
            };
            table.Children.Add(row);
        }

        expander.Content = table;
        return expander;
    }

    private UIElement RenderHeading(HeadingBlockIr heading, EditorViewModel? editor)
    {
        var block = new RichTextBlock { IsTextSelectionEnabled = true };
        var paragraph = new Paragraph
        {
            FontSize = zoom * (heading.Level switch { 1 => 32, 2 => 24, 3 => 20, 4 => 18, _ => 16 }),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        AddInlines(paragraph.Inlines, heading.Inlines, editor);
        block.Blocks.Add(paragraph);
        return block;
    }

    private UIElement RenderParagraph(ParagraphBlockIr paragraph, EditorViewModel? editor)
    {
        var block = new RichTextBlock { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        var p = new Paragraph();
        AddInlines(p.Inlines, paragraph.Inlines, editor);
        block.Blocks.Add(p);
        return block;
    }

    private UIElement RenderQuote(QuoteBlockIr quote, EditorViewModel? editor)
    {
        var inner = new StackPanel { Spacing = 8 };
        foreach (var child in quote.Children)
        {
            inner.Children.Add(RenderBlock(child, editor));
        }

        return new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 4, 0, 4),
            Child = inner
        };
    }

    private UIElement RenderEmbed(WikiEmbedInline embed, EditorViewModel? editor)
    {
        var label = embed.Target + (string.IsNullOrEmpty(embed.Heading) ? string.Empty : "#" + embed.Heading);
        var header = new HyperlinkButton
        {
            Content = embed.Missing
                ? Strings.Format(Strings.DefaultMap, "WikiEmbedMissing", label)
                : "[[" + label + "]]",
            Padding = new Thickness(0),
            FontSize = 12 * zoom
        };
        header.Click += async (_, _) =>
        {
            if (editor is null)
            {
                return;
            }

            await editor.OpenTargetAsync(embed.Url + (embed.Heading is null ? "" : "#" + embed.Heading));
        };

        var inner = new StackPanel { Spacing = 8 };
        inner.Children.Add(header);
        var previousDocumentPath = embeddedDocumentPath;
        embeddedDocumentPath = embed.Url;
        try
        {
            foreach (var child in embed.Children)
                inner.Children.Add(RenderBlock(child, editor));
        }
        finally { embeddedDocumentPath = previousDocumentPath; }

        return new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 4),
            Child = inner
        };
    }

    private UIElement RenderList(ListBlockIr list, EditorViewModel? editor)
    {
        var panel = new StackPanel { Spacing = 6 };
        var number = list.StartNumber;
        foreach (var item in list.Items)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            FrameworkElement marker;
            if (item.Checked is { } check)
            {
                // Embedded source lines belong to another document; edit it via the embed header.
                var box = new CheckBox { IsChecked = check, MinWidth = 32, IsEnabled = editor is not null && embeddedDocumentPath is null };
                var line = item.SourceLine;
                box.Click += (_, _) => editor?.SetTaskChecked(line, box.IsChecked == true);
                marker = box;
            }
            else
            {
                marker = new TextBlock
                {
                    Text = list.Ordered ? list.Marker(number) : "•",
                    Margin = new Thickness(0, 2, 8, 0),
                    MinWidth = 24
                };
            }

            number++;

            var content = new StackPanel { Spacing = 6 };
            foreach (var child in item.Children)
            {
                content.Children.Add(RenderBlock(child, editor));
            }

            Grid.SetColumn(content, 1);
            row.Children.Add(marker);
            row.Children.Add(content);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static UIElement RenderCode(CodeBlockIr code)
    {
        var language = (code.Language ?? string.Empty).Trim();
        var isDiagram = language.Equals("mermaid", StringComparison.OrdinalIgnoreCase) ||
                        language.Equals("plantuml", StringComparison.OrdinalIgnoreCase) ||
                        language.Equals("dot", StringComparison.OrdinalIgnoreCase);
        var header = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(language)
                ? Strings.T("CodeFence")
                : isDiagram ? language + " · " + Strings.T("DiagramStub") : language,
            Opacity = 0.7,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var body = new RichTextBlock
        {
            FontFamily = new FontFamily("Cascadia Code"),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap
        };
        var paragraph = new Paragraph();
        if (code.Tokens.Count == 0)
        {
            paragraph.Inlines.Add(new Run { Text = code.Code });
        }
        else
        {
            AddCodeRuns(paragraph.Inlines, code.Code, code.Tokens);
        }

        body.Blocks.Add(paragraph);
        var scroller = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(scroller);
        return new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = stack
        };
    }

    public static UIElement RenderGraph(GraphLayout layout, Action<string>? onNodeClick = null, bool lifeLines = false)
    {
        var canvas = new Canvas
        {
            Width = Math.Max(8, layout.Width),
            Height = Math.Max(8, layout.Height)
        };
        var stroke = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];
        var fill = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        if (lifeLines)
        {
            foreach (var node in layout.Nodes)
            {
                var x = node.X + node.Width / 2;
                canvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = node.Y + node.Height,
                    X2 = x,
                    Y2 = layout.Height,
                    Stroke = stroke,
                    StrokeThickness = 1
                });
            }
        }

        foreach (var edge in layout.Edges)
        {
            var line = new Line
            {
                X1 = edge.X1,
                Y1 = edge.Y1,
                X2 = edge.X2,
                Y2 = edge.Y2,
                Stroke = accent,
                StrokeThickness = 1.5
            };
            if (edge.Dashed)
            {
                line.StrokeDashArray = new DoubleCollection { 4, 4 };
            }

            canvas.Children.Add(line);
            if (!string.IsNullOrWhiteSpace(edge.Label))
            {
                var label = new TextBlock
                {
                    Text = edge.Label,
                    FontSize = 11,
                    Opacity = 0.85
                };
                Canvas.SetLeft(label, (edge.X1 + edge.X2) / 2 - 40);
                Canvas.SetTop(label, edge.Y1 - 16);
                canvas.Children.Add(label);
            }
        }

        foreach (var node in layout.Nodes)
        {
            var body = new Border
            {
                Width = node.Width,
                Height = node.Height,
                Background = fill,
                BorderBrush = accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = new TextBlock
                {
                    Text = node.Label,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(6, 0, 6, 0)
                },
                Tag = node.Id
            };
            Canvas.SetLeft(body, node.X);
            Canvas.SetTop(body, node.Y);
            if (onNodeClick is not null)
            {
                var id = node.Id;
                body.Tapped += (_, _) => onNodeClick(id);
            }

            canvas.Children.Add(body);
        }

        return canvas;
    }

    private static UIElement RenderMermaid(MermaidBlockIr mermaid)
    {
        UIElement content = mermaid.Diagram.Kind switch
        {
            MermaidKind.Pie => RenderPie(mermaid.Diagram),
            MermaidKind.Sequence => RenderGraph(GraphLayoutEngine.ForSequence(mermaid.Diagram), lifeLines: true),
            MermaidKind.Gantt => RenderGraph(GraphLayoutEngine.ForGantt(mermaid.Diagram)),
            _ => RenderGraph(GraphLayoutEngine.ForMermaid(mermaid.Diagram))
        };
        return new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = new ScrollViewer
            {
                Content = content,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
        };
    }

    private static UIElement RenderPie(MermaidDiagram diagram)
    {
        var slices = GraphLayoutEngine.ForPie(diagram);
        var stack = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrWhiteSpace(diagram.Title))
        {
            stack.Children.Add(new TextBlock
            {
                Text = diagram.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
        }

        if (slices.Count == 0)
        {
            stack.Children.Add(new TextBlock { Text = Strings.T("DiagramStub"), Opacity = 0.7 });
            return stack;
        }

        const double size = 180;
        const double radius = 80;
        const double cx = 90;
        const double cy = 90;
        var canvas = new Canvas { Width = size, Height = size };
        var palette = new[]
        {
            Windows.UI.Color.FromArgb(255, 0, 120, 212),
            Windows.UI.Color.FromArgb(255, 16, 124, 16),
            Windows.UI.Color.FromArgb(255, 196, 43, 28),
            Windows.UI.Color.FromArgb(255, 255, 185, 0),
            Windows.UI.Color.FromArgb(255, 136, 23, 152),
            Windows.UI.Color.FromArgb(255, 0, 153, 188)
        };
        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            var start = DegreesToPoint(cx, cy, radius, slice.StartDegrees);
            var end = DegreesToPoint(cx, cy, radius, slice.StartDegrees + slice.SweepDegrees);
            var figure = new PathFigure { StartPoint = new Windows.Foundation.Point(cx, cy), IsClosed = true };
            figure.Segments.Add(new LineSegment { Point = start });
            figure.Segments.Add(new ArcSegment
            {
                Size = new Windows.Foundation.Size(radius, radius),
                Point = end,
                IsLargeArc = slice.SweepDegrees > 180,
                SweepDirection = SweepDirection.Clockwise
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            canvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = geometry,
                Fill = new SolidColorBrush(palette[i % palette.Length])
            });
        }

        stack.Children.Add(canvas);
        foreach (var slice in slices)
        {
            stack.Children.Add(new TextBlock
            {
                Text = slice.Label + " · " + slice.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                Opacity = 0.85
            });
        }

        return stack;
    }

    private static Windows.Foundation.Point DegreesToPoint(double cx, double cy, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new Windows.Foundation.Point(cx + radius * Math.Cos(radians), cy + radius * Math.Sin(radians));
    }

    private static void AddCodeRuns(InlineCollection target, string code, IReadOnlyList<CodeToken> tokens)
    {
        var ordered = tokens.OrderBy(token => token.Start).ToList();
        var cursor = 0;
        foreach (var token in ordered)
        {
            if (token.Start > code.Length || token.Length <= 0)
            {
                continue;
            }

            var start = Math.Clamp(token.Start, 0, code.Length);
            if (start > cursor)
            {
                target.Add(new Run { Text = code[cursor..start] });
            }

            var end = Math.Clamp(start + token.Length, start, code.Length);
            if (end > start)
            {
                target.Add(new Run
                {
                    Text = code[start..end],
                    Foreground = new SolidColorBrush(ColorForToken(token.Kind))
                });
            }

            cursor = Math.Max(cursor, end);
        }

        if (cursor < code.Length)
        {
            target.Add(new Run { Text = code[cursor..] });
        }
    }

    private static Windows.UI.Color ColorForToken(CodeTokenKind kind)
    {
        var dark = App.MainWindow?.Content is FrameworkElement root
            ? root.ActualTheme == ElementTheme.Dark
            : Application.Current.RequestedTheme == ApplicationTheme.Dark;
        return kind switch
        {
            CodeTokenKind.Keyword => dark ? Windows.UI.Color.FromArgb(255, 86, 156, 214) : Windows.UI.Color.FromArgb(255, 0, 0, 255),
            CodeTokenKind.String => dark ? Windows.UI.Color.FromArgb(255, 206, 145, 120) : Windows.UI.Color.FromArgb(255, 163, 21, 21),
            CodeTokenKind.Comment => dark ? Windows.UI.Color.FromArgb(255, 87, 166, 74) : Windows.UI.Color.FromArgb(255, 0, 128, 0),
            CodeTokenKind.Number => dark ? Windows.UI.Color.FromArgb(255, 181, 206, 168) : Windows.UI.Color.FromArgb(255, 9, 134, 88),
            CodeTokenKind.TypeName => dark ? Windows.UI.Color.FromArgb(255, 78, 201, 176) : Windows.UI.Color.FromArgb(255, 43, 145, 175),
            CodeTokenKind.Punctuation => dark ? Windows.UI.Color.FromArgb(255, 200, 200, 200) : Windows.UI.Color.FromArgb(255, 80, 80, 80),
            _ => dark ? Windows.UI.Color.FromArgb(255, 220, 220, 220) : Windows.UI.Color.FromArgb(255, 30, 30, 30)
        };
    }

    private UIElement RenderTable(TableBlockIr table, EditorViewModel? editor)
    {
        var grid = new Grid();
        var columns = Math.Min(PreviewBuilder.MaxTableColumns, table.Grid.TotalColumns);
        for (var c = 0; c < columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var rowIndex = 0;
        void AddRow(IReadOnlyList<IReadOnlyList<IReadOnlyList<PreviewInline>>> source, bool header)
        {
            foreach (var row in source)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (var c = 0; c < Math.Min(columns, row.Count); c++)
                {
                    var cell = new Border
                    {
                        BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Padding = new Thickness(8, 4, 8, 4)
                    };
                    var text = new RichTextBlock { IsTextSelectionEnabled = true };
                    var p = new Paragraph();
                    if (header)
                    {
                        p.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                    }

                    if (c < table.Grid.Alignments.Count)
                    {
                        p.TextAlignment = table.Grid.Alignments[c] switch
                        {
                            TableAlignment.Center => TextAlignment.Center,
                            TableAlignment.Right => TextAlignment.Right,
                            _ => TextAlignment.Left
                        };
                    }

                    AddInlines(p.Inlines, row[c], editor);
                    text.Blocks.Add(p);
                    cell.Child = text;
                    Grid.SetRow(cell, rowIndex);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }

                rowIndex++;
            }
        }

        AddRow(table.Grid.Header, true);
        AddRow(table.Grid.Rows, false);
        var scroller = new ScrollViewer
        {
            Content = grid,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        if (table.Grid.Truncated)
        {
            var stack = new StackPanel();
            stack.Children.Add(scroller);
            stack.Children.Add(new TextBlock
            {
                Text = Strings.Format(Strings.DefaultMap, "TableTruncated", table.Grid.TotalRows, table.Grid.TotalColumns),
                Opacity = 0.7,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return stack;
        }

        return scroller;
    }

    private UIElement RenderFootnote(FootnoteBlockIr note, EditorViewModel? editor)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = "[" + note.Label + "]",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.8
        });
        foreach (var child in note.Children)
        {
            stack.Children.Add(RenderBlock(child, editor));
        }

        return stack;
    }

    private UIElement RenderFigure(FigureBlockIr figure, EditorViewModel? editor)
    {
        var stack = new StackPanel { Spacing = 8 };
        foreach (var child in figure.Children)
        {
            stack.Children.Add(RenderBlock(child, editor));
        }

        if (figure.Caption is { Count: > 0 } caption)
        {
            var text = new RichTextBlock { Opacity = 0.75 };
            var p = new Paragraph();
            AddInlines(p.Inlines, caption, editor);
            text.Blocks.Add(p);
            stack.Children.Add(text);
        }

        return stack;
    }

    private UIElement RenderDefinitions(DefinitionListIr list, EditorViewModel? editor)
    {
        var stack = new StackPanel { Spacing = 8 };
        foreach (var item in list.Items)
        {
            var term = new RichTextBlock();
            var p = new Paragraph { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            AddInlines(p.Inlines, item.Term, editor);
            term.Blocks.Add(p);
            stack.Children.Add(term);
            foreach (var definition in item.Definitions)
            {
                stack.Children.Add(RenderBlock(definition, editor));
            }
        }

        return stack;
    }

    private UIElement RenderContainer(CustomContainerIr container, EditorViewModel? editor)
    {
        var inner = new StackPanel { Spacing = 8 };
        foreach (var child in container.Children)
        {
            inner.Children.Add(RenderBlock(child, editor));
        }

        var kind = (container.Info ?? string.Empty).Trim().ToLowerInvariant();
        var backgroundKey = kind switch
        {
            "warning" or "warn" or "caution" => "SystemFillColorCautionBackgroundBrush",
            "danger" or "error" => "SystemFillColorCriticalBackgroundBrush",
            "tip" or "success" or "ok" => "SystemFillColorSuccessBackgroundBrush",
            "info" or "note" => "SystemFillColorAttentionBackgroundBrush",
            _ => "CardBackgroundFillColorDefaultBrush"
        };
        Brush background;
        try
        {
            background = (Brush)Application.Current.Resources[backgroundKey];
        }
        catch
        {
            background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
        }

        if (!string.IsNullOrEmpty(container.Info))
        {
            inner.Children.Insert(0, new TextBlock
            {
                Text = container.Info,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Opacity = 0.8
            });
        }

        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = inner
        };
    }

    private static UIElement RenderStub(string kind, string preview)
        => new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(preview)
                    ? Strings.Format(Strings.DefaultMap, "PreviewStub", kind)
                    : preview,
                FontFamily = new FontFamily("Cascadia Code"),
                TextWrapping = TextWrapping.Wrap
            }
        };

    private void AddInlines(InlineCollection target, IReadOnlyList<PreviewInline> inlines, EditorViewModel? editor = null)
    {
        foreach (var inline in inlines)
        {
            foreach (var element in CreateInlines(inline, editor))
            {
                target.Add(element);
            }
        }
    }

    private IEnumerable<Microsoft.UI.Xaml.Documents.Inline> CreateInlines(PreviewInline inline, EditorViewModel? editor)
    {
        switch (inline)
        {
            case TextInline text:
                yield return new Run { Text = text.Text };
                yield break;
            case StrongInline strong:
                foreach (var child in WrapSpan(strong.Children, span => span.FontWeight = Microsoft.UI.Text.FontWeights.Bold, editor))
                {
                    yield return child;
                }

                yield break;
            case EmphasisInline em:
                foreach (var child in WrapSpan(em.Children, span => span.FontStyle = Windows.UI.Text.FontStyle.Italic, editor))
                {
                    yield return child;
                }

                yield break;
            case StrikethroughInline strike:
                foreach (var child in WrapSpan(strike.Children, span => { }, editor))
                {
                    if (child is Span span)
                    {
                        span.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
                    }

                    yield return child;
                }

                yield break;
            case CodeInline code:
                yield return new Run
                {
                    Text = code.Code,
                    FontFamily = new FontFamily("Cascadia Code")
                };
                yield break;
            case LinkInline { IsImage: true } image:
                yield return CreateImage(image);
                yield break;
            case LinkInline link:
                var (path, heading) = LinkResolver.SplitFragment(link.Url ?? string.Empty);
                if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(path, UriKind.Absolute, out var localUri))
                    path = localUri.LocalPath;
                var sourcePath = embeddedDocumentPath;
                var hyper = new Hyperlink();
                hyper.Click += async (_, _) =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(link.Url) && link.Url.StartsWith('#') && editor is not null)
                        {
                            var anchor = link.Url.TrimStart('#');
                            if (sourcePath is not null) await editor.OpenTargetAsync(sourcePath + "#" + anchor);
                            else editor.GoToHeading(anchor);
                            return;
                        }

                        if (Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) &&
                            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto))
                        {
                            await Launcher.LaunchUriAsync(uri);
                            return;
                        }

                        if (ArchiveAssets.IsAttachmentUri(path))
                        {
                            await ArchiveAssets.OpenAsync(path);
                            return;
                        }
                        if (editor is not null) await editor.OpenTargetAsync(link.Url ?? string.Empty);

                    }
                    catch (Exception ex)
                    {
                        DiagnosticsService.LogError("Preview", "Apertura link fallita", ex);
                    }
                };
                foreach (var child in link.Children.SelectMany(child => CreateInlines(child, editor)))
                {
                    hyper.Inlines.Add(child);
                }

                if (hyper.Inlines.Count == 0)
                {
                    hyper.Inlines.Add(new Run { Text = link.Url });
                }

                if (!string.IsNullOrEmpty(path) &&
                    !path.StartsWith('#') &&
                    !LinkResolver.IsRemote(path) &&
                    DocumentStore.IsMarkdown(path) &&
                    !File.Exists(path))
                {
                    hyper.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                }

                yield return hyper;
                yield break;
            case WikiEmbedInline embed:
                yield return new InlineUIContainer { Child = RenderEmbed(embed, editor) };
                yield break;
            case LineBreakInline { IsHard: false }:
                yield return new Run { Text = " " };
                yield break;
            case LineBreakInline:
                yield return new LineBreak();
                yield break;
            case MathInline math:
                yield return new Run
                {
                    Text = MathText.ToDisplay(math.Expression),
                    FontFamily = new FontFamily("Cambria Math")
                };
                yield break;
            case HtmlInlineStub stub:
                yield return new Run
                {
                    Text = stub.Tag,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                };
                yield break;
            case FootnoteRefInline note:
                var footnotePath = embeddedDocumentPath;
                var footnoteLink = new Hyperlink();
                footnoteLink.Click += async (_, _) =>
                {
                    if (editor is null) return;
                    try
                    {
                        // Embedded footnotes live in the embedded note: open it, never import it.
                        if (footnotePath is not null)
                        {
                            var before = editor.CurrentNote;
                            await editor.OpenTargetAsync(footnotePath);
                            if (ReferenceEquals(before, editor.CurrentNote)) return;
                        }

                        editor.GoToFootnote(note.Label);
                    }
                    catch (Exception ex)
                    {
                        DiagnosticsService.LogError("Preview", "Nota a piè di pagina non raggiungibile", ex);
                    }
                };
                footnoteLink.Inlines.Add(new Run { Text = "[" + note.Label + "]", FontSize = 12 });
                yield return footnoteLink;
                yield break;
            case MarkedInline marked:
                foreach (var child in WrapSpan(marked.Children, span =>
                {
                    span.Foreground = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
                }, editor))
                {
                    yield return child;
                }

                yield break;
            case SuperscriptInline sup:
                foreach (var child in WrapSpan(sup.Children, span => span.FontSize = 12, editor))
                {
                    yield return child;
                }

                yield break;
            case SubscriptInline sub:
                foreach (var child in WrapSpan(sub.Children, span => span.FontSize = 12, editor))
                {
                    yield return child;
                }

                yield break;
            case InsertedInline inserted:
                foreach (var child in WrapSpan(inserted.Children, span =>
                {
                    span.TextDecorations = Windows.UI.Text.TextDecorations.Underline;
                }, editor))
                {
                    yield return child;
                }

                yield break;
            default:
                yield break;
        }
    }

    private IEnumerable<Microsoft.UI.Xaml.Documents.Inline> WrapSpan(
        IReadOnlyList<PreviewInline> children,
        Action<Span> configure,
        EditorViewModel? editor)
    {
        var span = new Span();
        configure(span);
        foreach (var child in children.SelectMany(item => CreateInlines(item, editor)))
        {
            span.Inlines.Add(child);
        }

        yield return span;
    }

    private static InlineUIContainer CreateImage(LinkInline image)
    {
        var container = new InlineUIContainer();
        if (LinkResolver.IsRemote(image.Url) && !SettingsService.Instance.Preview.LoadRemoteImages)
        {
            container.Child = new HyperlinkButton { Content = image.Url };
            return container;
        }

        try
        {
            var bitmap = new BitmapImage();
            if (ArchiveAssets.IsAttachmentUri(image.Url))
            {
                _ = ArchiveAssets.LoadImageAsync(bitmap, image.Url);
            }
            else if (LinkResolver.IsRemote(image.Url))
            {
                bitmap.UriSource = new Uri(image.Url);
            }
            else
            {
                var local = File.Exists(image.Url)
                    ? image.Url
                    : LinkResolver.TryResolveLocal(image.Url, documentPath: null);
                if (local is null || !LinkResolver.IsSafeLocalImage(local))
                {
                    container.Child = new TextBlock { Text = image.Url };
                    return container;
                }

                bitmap.UriSource = new Uri(Path.GetFullPath(local));
            }

            var preview = new Image
            {
                Source = bitmap,
                MaxHeight = 360,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var flyout = new Flyout
            {
                Content = new ScrollViewer
                {
                    ZoomMode = ZoomMode.Enabled,
                    MinZoomFactor = 0.5f,
                    MaxZoomFactor = 4,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Width = 720,
                    Height = 520,
                    Content = new Image
                    {
                        Source = bitmap,
                        Stretch = Stretch.Uniform
                    }
                },
                Placement = FlyoutPlacementMode.Bottom
            };
            preview.Tapped += (_, args) =>
            {
                args.Handled = true;
                flyout.ShowAt(preview);
            };
            container.Child = preview;
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Preview", "Immagine non caricata", ex);
            container.Child = new TextBlock { Text = image.Url };
        }

        return container;
    }
}

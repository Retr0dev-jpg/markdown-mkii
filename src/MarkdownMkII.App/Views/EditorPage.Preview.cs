using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private void ApplyHighlight()
    {
        if (ViewModel.CurrentNote is null)
        {
            return;
        }

        if (composing)
        {
            highlightTimer.Stop();
            highlightTimer.Start();
            return;
        }

        try
        {
            // A command updates the model before the control; painting now would use ranges of the old story.
            EditorBox.Document.GetText(Microsoft.UI.Text.TextGetOptions.None, out var shown);
            if (ReadEditorText(shown) != ViewModel.CurrentNote.Text)
            {
                return;
            }

            highlighter.Apply(
                EditorBox,
                ViewModel.CurrentNote.Text,
                MarkdownMkII.Core.DocumentParser.IsLarge(ViewModel.CurrentNote.Text),
                ViewModel.CurrentNote.Parsed?.Text == ViewModel.CurrentNote.Text ? ViewModel.CurrentNote.Parsed.Syntax : null,
                FindBar.Visibility == Visibility.Visible && !string.IsNullOrEmpty(FindBox.Text)
                    ? FindReplace.FindAll(ViewModel.CurrentNote.Text, FindBox.Text, CurrentFindOptions())
                    : null,
                HeadingFold.HiddenRanges(
                    ViewModel.CurrentNote.Text,
                    ViewModel.CurrentNote.Outline,
                    ViewModel.CurrentNote.FoldedHeadings));
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Evidenziazione non applicata", ex);
        }
    }

    private bool previewRefreshPending;
    private bool previewRefreshForce;

    private async void RefreshPreview(bool force = false)
    {
        if (isRefreshingPreview)
        {
            // Re-run once the current refresh ends instead of dropping this request.
            previewRefreshPending = true;
            previewRefreshForce |= force;
            return;
        }

        if (NoteSecurity.Blocking || !IsLoaded)
        {
            return;
        }

        var tab = ViewModel.CurrentNote;
        if (tab is null)
        {
            PreviewHost.Content = null;
            return;
        }

        if (!force && !SettingsService.Instance.Preview.LivePreview)
        {
            return;
        }

        if (tab.ViewMode == MarkdownMkII.Core.Models.EditorViewMode.Editor)
        {
            // Keep the outline current without building a hidden native visual
            // tree (especially expensive for multi-megabyte paragraphs).
            if (tab.Parsed?.Text != tab.Text) tab.Reparse();
            BindOutline();
            TagsLine.Text = tab.Metadata?.Tags.Count > 0
                ? string.Join("  ", tab.Metadata.Tags.Select(tag => "#" + tag)) : string.Empty;
            UpdateStatus();
            return;
        }

        isRefreshingPreview = true;
        try
        {
            var session = NoteArchive.Database.SessionVersion;
            var text = tab.Text;
            if (tab.NoteId is { } noteId) tab.NoteResources = await ArchiveAssets.PreviewContextAsync(noteId, text);
            if (!IsLoaded) return;
            if (NoteSecurity.Blocking || session != NoteArchive.Database.SessionVersion || !ReferenceEquals(ViewModel.CurrentNote, tab) || tab.Text != text || tab.ViewMode == MarkdownMkII.Core.Models.EditorViewMode.Editor)
            {
                parseTimer.Stop(); parseTimer.Start(); return;
            }
            tab.Reparse();
            var offset = PreviewScroller.VerticalOffset;
            if (tab.DiffMode)
            {
                try
                {
                    var other = tab.DiffAgainstText;

                    PreviewHost.Content = other is null
                        ? new TextBlock { Text = Strings.T("CompareFailed"), Opacity = 0.7 }
                        : RenderDiff(TextDiff.Lines(tab.Text, other));
                }
                catch (IOException)
                {
                    PreviewHost.Content = new TextBlock { Text = Strings.T("CompareFailed"), Opacity = 0.7 };
                }
            }
            else if (tab.KanbanMode)
            {
                PreviewHost.Content = RenderKanban(MarkdownKanban.Extract(tab.Text));
            }
            else if (tab.Preview is not null)
            {
                var blocks = tab.Preview.Blocks
                    .Where(block => !HeadingFold.IsLineHidden(tab.Outline, tab.FoldedHeadings, block.SourceLine, tab.Text))
                    .ToList();
                var preview = new PreviewDocument(blocks, tab.Preview.FrontMatter, tab.Preview.FrontMatterRaw);
                if (tab.SlideIndex >= 0)
                {
                    var slides = ScrollMapper.Slides(preview.Blocks);
                    if (slides.Count == 0)
                    {
                        PreviewHost.Content = previewRenderer.Render(preview, ViewModel, SettingsService.Instance.Preview.Zoom);
                    }
                    else
                    {
                        var index = Math.Clamp(tab.SlideIndex, 0, slides.Count - 1);
                        var page = new PreviewDocument(slides[index], preview.FrontMatter, preview.FrontMatterRaw);
                        var stack = new StackPanel { Spacing = 12 };
                        stack.Children.Add(new TextBlock
                        {
                            Text = Strings.Format(Strings.DefaultMap, "SlideIndex", index + 1, slides.Count),
                            Opacity = 0.7
                        });
                        stack.Children.Add(previewRenderer.Render(page, ViewModel, SettingsService.Instance.Preview.Zoom));
                        PreviewHost.Content = stack;
                    }
                }
                else
                {
                    PreviewHost.Content = previewRenderer.Render(preview, ViewModel, SettingsService.Instance.Preview.Zoom);
                }
            }

            BindOutline();
            TagsLine.Text = tab.Metadata?.Tags.Count > 0
                ? string.Join("  ", tab.Metadata.Tags.Select(tag => "#" + tag))
                : string.Empty;
            UpdateStatus();
            _ = RefreshOutgoingAsync();
            if (SettingsService.Instance.Preview.SyncScroll)
            {
                SyncPreviewToEditor();
            }
            else
            {
                PreviewScroller.UpdateLayout();
                PreviewScroller.ChangeView(null, offset, null, true);
            }
        }
        catch (OperationCanceledException)
        {
            // Lock or session change: the next unlock or edit schedules a fresh render.
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Preview", "Anteprima non aggiornata", ex);
        }
        finally
        {
            isRefreshingPreview = false;
            if (previewRefreshPending)
            {
                var pendingForce = previewRefreshForce;
                previewRefreshPending = previewRefreshForce = false;
                DispatcherQueue.TryEnqueue(() => RefreshPreview(pendingForce));
            }
        }
    }

    private bool gutterRefreshQueued;
    private void RebuildLineNumbers()
    {
        if (gutterRefreshQueued) return;
        gutterRefreshQueued = true;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            gutterRefreshQueued = false;
            RenderLineNumbers();
        });
    }

    private void RenderLineNumbers()
    {
        if (!IsLoaded || !SettingsService.Instance.Editor.ShowLineNumbers || ViewModel.CurrentNote is not { } note) { LineNumbers.Children.Clear(); return; }
        var visibleCount = 0;
        var map = note.Metrics.Lines;
        var font = SettingsService.Instance.Editor;
        var width = Math.Max(32, map.LineCount.ToString().Length * font.FontSize * 0.65 + 16);
        LineNumbers.Width = width;
        LineNumbers.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, width, Math.Max(0, EditorBox.ActualHeight)) };
        try
        {
            // Native ranges account for wrapped lines and hidden (folded) sections.
            // Draw only the visible logical lines instead of allocating a label for the entire note.
            var first = Math.Max(0, FirstVisibleLine(note.Text) - 1);
            var previousY = double.MinValue;
            for (var line = first; line < map.LineCount && visibleCount < 200; line++)
            {
                var offset = map.OffsetOfLine(line);
                var range = EditorBox.Document.GetRange(offset, offset);
                range.GetRect(Microsoft.UI.Text.PointOptions.Transform, out var rect, out _);
                if (rect.Y > EditorBox.ActualHeight) break;
                if (rect.Y < 0 || rect.Y <= previousY + 0.5) continue;
                previousY = rect.Y;
                var label = visibleCount < LineNumbers.Children.Count ? (TextBlock)LineNumbers.Children[visibleCount] : new TextBlock();
                label.Text = (line + 1).ToString(); label.FontSize = font.FontSize;
                label.FontFamily = EditorBox.FontFamily; label.Width = width - 8; label.TextAlignment = TextAlignment.Right;
                label.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
                range.GetPoint(Microsoft.UI.Text.HorizontalCharacterAlignment.Left,
                    Microsoft.UI.Text.VerticalCharacterAlignment.Baseline,
                    Microsoft.UI.Text.PointOptions.Transform | Microsoft.UI.Text.PointOptions.ClientCoordinates, out var baseline);
                range.GetPoint(Microsoft.UI.Text.HorizontalCharacterAlignment.Left,
                    Microsoft.UI.Text.VerticalCharacterAlignment.Top,
                    Microsoft.UI.Text.PointOptions.Transform | Microsoft.UI.Text.PointOptions.ClientCoordinates, out var top);
                // Range rectangles exclude the RichEditBox padding. GetPoint uses host coordinates,
                // so take only its baseline-to-top distance before positioning in the gutter.
                Canvas.SetTop(label, EditorBox.Padding.Top + rect.Y + baseline.Y - top.Y - label.BaselineOffset);
                if (visibleCount == LineNumbers.Children.Count) LineNumbers.Children.Add(label);
                visibleCount++;
            }
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // A detached RichEdit document has no layout; the next load/resize redraws the gutter.
        }
        while (LineNumbers.Children.Count > visibleCount) LineNumbers.Children.RemoveAt(LineNumbers.Children.Count - 1);
    }

    private void UpdateStatus()
    {
        var tab = ViewModel.CurrentNote;
        if (tab is null)
        {
            StatusPath.Text = string.Empty;
            StatusWords.Text = string.Empty;
            StatusChars.Text = string.Empty;
            StatusRead.Text = string.Empty;
            StatusLine.Text = string.Empty;
            StatusHeading.Text = string.Empty;
            StatusEncoding.Text = string.Empty;
            return;
        }

        var (start, length) = GetSelection();
        var stats = tab.Metrics.Statistics.IsCompletedSuccessfully ? tab.Metrics.Statistics.Result : tab.Stats;
        var map = tab.Metrics.Lines;
        QueueSelectionStatistics();
        var line = map.LineOfOffset(start);
        var column = start - map.OffsetOfLine(line);
        StatusPath.Text = tab.Title;
        StatusLine.Text = $"{line + 1}:{column + 1}";
        var crumb = TocExtractor.Breadcrumb(tab.Outline, line);
        StatusHeading.Text = crumb.Count == 0 ? string.Empty : string.Join(" › ", crumb);
        // Selection statistics own these fields. Save-state notifications must not
        // overwrite a completed selection count with the cached document totals.
        if (length == 0)
        {
            StatusWords.Text = stats.Words + " " + Strings.T("Words");
            StatusChars.Text = stats.Characters + " " + Strings.T("Characters");
        }
        StatusRead.Text = "~" + Math.Max(1, (int)Math.Ceiling(stats.ReadingTime.TotalMinutes)) + " min";
        StatusEncoding.Text = ViewModel.SaveStatus;
        RetrySaveButton.Visibility = ViewModel.SaveError is null ? Visibility.Collapsed : Visibility.Visible;
        var issues = tab.Parsed?.Diagnostics.Count ?? 0;
        if (issues > 0)
        {
            StatusEncoding.Text += " · " + Strings.Format(Strings.DefaultMap, "IssueCount", issues);
        }
        if (tab.IsLargeFile)
        {
            StatusRead.Text += " · " + Strings.T("LargeFile");
        }
    }

    private UIElement RenderKanban(IReadOnlyList<KanbanColumn> columns)
    {
        if (columns.Count == 0)
        {
            return new TextBlock { Text = Strings.T("KanbanEmpty"), Opacity = 0.7 };
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var column in columns)
        {
            var stack = new StackPanel { Spacing = 8, Width = 220 };
            stack.Children.Add(new TextBlock
            {
                Text = column.Name + " (" + column.Cards.Count + ")",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            foreach (var card in column.Cards)
            {
                var line = card.Line;
                var body = new StackPanel { Spacing = 4 };
                body.Children.Add(new TextBlock
                {
                    Text = (card.Done ? "☑ " : "☐ ") + card.Title,
                    TextWrapping = TextWrapping.Wrap
                });
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var left = new Button { Content = "←" };
                var right = new Button { Content = "→" };
                left.Click += (_, _) => ViewModel.MoveKanbanCard(line, -1);
                right.Click += (_, _) => ViewModel.MoveKanbanCard(line, 1);
                actions.Children.Add(left);
                actions.Children.Add(right);
                body.Children.Add(actions);
                var border = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Child = body
                };
                border.Tapped += (_, _) => ViewModel.CycleKanbanCard(line);
                stack.Children.Add(border);
            }

            row.Children.Add(stack);
        }

        return new ScrollViewer
        {
            Content = row,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static UIElement RenderDiff(IReadOnlyList<DiffLine> lines)
    {
        var stack = new StackPanel { Spacing = 2 };
        var limit = Math.Min(lines.Count, 400);
        for (var i = 0; i < limit; i++)
        {
            var line = lines[i];
            var color = line.Kind switch
            {
                '+' => Windows.UI.Color.FromArgb(40, 16, 124, 16),
                '-' => Windows.UI.Color.FromArgb(40, 196, 43, 28),
                _ => Windows.UI.Color.FromArgb(0, 0, 0, 0)
            };
            stack.Children.Add(new Border
            {
                Background = new SolidColorBrush(color),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new TextBlock
                {
                    Text = line.Kind + " " + line.Text,
                    FontFamily = new FontFamily("Cascadia Code"),
                    TextWrapping = TextWrapping.Wrap
                }
            });
        }

        if (lines.Count > limit)
        {
            stack.Children.Add(new TextBlock { Text = "…", Opacity = 0.7 });
        }

        return stack;
    }
}

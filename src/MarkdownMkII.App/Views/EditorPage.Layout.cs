using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private void ApplyViewMode(EditorViewMode mode)
    {
        var editor = SettingsService.Instance.Editor;
        var split = mode is EditorViewMode.Split or EditorViewMode.SplitVertical;
        EditorColumn.Width = mode == EditorViewMode.Preview
            ? new GridLength(0)
            : new GridLength(editor.EditorPaneStar, GridUnitType.Star);
        PreviewColumn.Width = mode == EditorViewMode.Editor
            ? new GridLength(0)
            : new GridLength(editor.PreviewPaneStar, GridUnitType.Star);
        EditorPane.Visibility = mode == EditorViewMode.Preview ? Visibility.Collapsed : Visibility.Visible;
        EditorBox.Visibility = mode == EditorViewMode.Preview ? Visibility.Collapsed : Visibility.Visible;
        LineNumberScroller.Visibility = mode == EditorViewMode.Preview || !editor.ShowLineNumbers
            ? Visibility.Collapsed
            : Visibility.Visible;
        PreviewScroller.Visibility = mode == EditorViewMode.Editor ? Visibility.Collapsed : Visibility.Visible;
        EditorSplitter.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
        LayoutEditorPreview(mode);
        var focus = ViewModel.CurrentNote?.FocusMode == true;
        var showOutline = SettingsService.Instance.Editor.ShowOutline && !focus;
        HeadingRailColumn.Width = new GridLength(showOutline ? 14 : 0);
        HeadingRailCanvas.Visibility = showOutline ? Visibility.Visible : Visibility.Collapsed;
        OutlineSplitColumn.Width = new GridLength(showOutline ? 6 : 0);
        OutlineColumn.Width = showOutline
            ? new GridLength(editor.OutlinePaneWidth)
            : new GridLength(0);
        PreviewSplitter.Visibility = showOutline ? Visibility.Visible : Visibility.Collapsed;
        OutlinePanel.Visibility = showOutline ? Visibility.Visible : Visibility.Collapsed;
        MinimapColumn.Width = editor.ShowMinimap && !focus ? new GridLength(76) : new GridLength(0);
        MinimapPanel.Visibility = editor.ShowMinimap && !focus ? Visibility.Visible : Visibility.Collapsed;
        DispatcherQueue.TryEnqueue(RebuildMinimap);
    }

    private void LayoutEditorPreview(EditorViewMode mode)
    {
        var vertical = mode == EditorViewMode.SplitVertical;
        var editor = SettingsService.Instance.Editor;
        if (vertical)
        {
            Grid.SetColumn(EditorPane, 0);
            Grid.SetColumnSpan(EditorPane, 3);
            Grid.SetRow(EditorPane, 0);
            Grid.SetRowSpan(EditorPane, 1);
            Grid.SetColumn(EditorSplitter, 0);
            Grid.SetColumnSpan(EditorSplitter, 3);
            Grid.SetRow(EditorSplitter, 1);
            Grid.SetRowSpan(EditorSplitter, 1);
            Grid.SetColumn(PreviewScroller, 0);
            Grid.SetColumnSpan(PreviewScroller, 3);
            Grid.SetRow(PreviewScroller, 2);
            Grid.SetRowSpan(PreviewScroller, 1);
            EditorSplitCol.Width = new GridLength(0);
            EditorSplitRow.Height = new GridLength(6);
            EditorRow.Height = new GridLength(editor.EditorPaneStar, GridUnitType.Star);
            PreviewRow.Height = new GridLength(editor.PreviewPaneStar, GridUnitType.Star);
        }
        else
        {
            Grid.SetColumn(EditorPane, 0);
            Grid.SetColumnSpan(EditorPane, 1);
            Grid.SetRow(EditorPane, 0);
            Grid.SetRowSpan(EditorPane, 3);
            Grid.SetColumn(EditorSplitter, 1);
            Grid.SetColumnSpan(EditorSplitter, 1);
            Grid.SetRow(EditorSplitter, 0);
            Grid.SetRowSpan(EditorSplitter, 3);
            Grid.SetColumn(PreviewScroller, 2);
            Grid.SetColumnSpan(PreviewScroller, 1);
            Grid.SetRow(PreviewScroller, 0);
            Grid.SetRowSpan(PreviewScroller, 3);
            EditorSplitCol.Width = new GridLength(mode == EditorViewMode.Split ? 6 : 0);
            EditorSplitRow.Height = new GridLength(0);
            EditorRow.Height = new GridLength(1, GridUnitType.Star);
            PreviewRow.Height = new GridLength(1, GridUnitType.Star);
        }

        if (EditorSplitter.Child is Rectangle bar)
        {
            if (vertical)
            {
                bar.Width = double.NaN;
                bar.Height = 1;
                bar.HorizontalAlignment = HorizontalAlignment.Stretch;
                bar.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                bar.Width = 1;
                bar.Height = double.NaN;
                bar.HorizontalAlignment = HorizontalAlignment.Center;
                bar.VerticalAlignment = VerticalAlignment.Stretch;
            }
        }
    }

    private void ApplyEditorSettings()
    {
        var editor = SettingsService.Instance.Editor;
        EditorBox.TextWrapping = editor.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        EditorBox.FontFamily = new FontFamily(editor.FontFamily);
        EditorBox.FontSize = editor.FontSize;
        try
        {
            var spacing = (float)Math.Clamp(editor.LineHeight <= 0 ? 1.35 : editor.LineHeight, 1.0, 2.5);
            EditorBox.Document.GetDefaultParagraphFormat().SetLineSpacing(
                Microsoft.UI.Text.LineSpacingRule.Multiple,
                spacing);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Interlinea non applicata", ex);
        }
        EditorBox.IsSpellCheckEnabled = editor.SpellCheck;
        EditorBox.DisabledFormattingAccelerators = DisabledFormattingAccelerators.All;
        // History lives in TextUndoHistory; a native stack would keep extra copies of deleted text.
        EditorBox.Document.UndoLimit = 0;
        LineNumberColumn.Width = editor.ShowLineNumbers ? GridLength.Auto : new GridLength(0);
        LineNumberScroller.Visibility = editor.ShowLineNumbers ? Visibility.Visible : Visibility.Collapsed;
        MinimapColumn.Width = editor.ShowMinimap ? new GridLength(76) : new GridLength(0);
        MinimapPanel.Visibility = editor.ShowMinimap ? Visibility.Visible : Visibility.Collapsed;
        highlightTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, editor.HighlightDebounceMs));
        parseTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, SettingsService.Instance.Preview.LivePreviewDelayMs));
        RebuildLineNumbers();
        ApplyPreviewZoom();
        UpdateCurrentLineHighlight();
    }

    private void ApplyPreviewZoom()
    {
        if (ViewModel.CurrentNote?.Preview is not null)
        {
            RefreshPreview(force: true);
        }
    }

    private void RebuildMinimap()
    {
        if (MinimapCanvas is null || MinimapViewport is null) return;
        var tab = ViewModel.CurrentNote;
        var height = MinimapCanvas.ActualHeight;
        var width = MinimapCanvas.ActualWidth;
        if (tab is null || height <= 1 || width <= 1 || !SettingsService.Instance.Editor.ShowMinimap)
        {
            MinimapCanvas.Children.Clear();
            MinimapViewport.Visibility = Visibility.Collapsed;
            minimapText = null;
            return;
        }
        // Cache the miniature: scrolling only moves the viewport, even in very large files.
        if (!ReferenceEquals(minimapText, tab.Text) || minimapHeight != height || minimapWidth != width)
        {
            minimapText = tab.Text;
            minimapHeight = height;
            minimapWidth = width;
            MinimapCanvas.Children.Clear();
            var map = tab.Metrics.Lines;
            miniatureExtent = Math.Min(height, Math.Max(4, map.LineCount * 4.0));
            var step = Math.Max(1, (int)Math.Ceiling(map.LineCount / 400.0));
            for (var line = 0; line < map.LineCount; line += step)
            {
                var text = map.LineText(line);
                if (string.IsNullOrWhiteSpace(text)) continue;
                var heading = text.TrimStart().StartsWith('#');
                var glyph = new TextBlock
                {
                    Text = text[..Math.Min(text.Length, 90)].Replace("\t", "  "),
                    FontFamily = new FontFamily("Cascadia Code"),
                    FontSize = 3,
                    LineHeight = 4,
                    MaxWidth = width,
                    MaxHeight = Math.Max(1, Math.Min(4, miniatureExtent * step / map.LineCount)),
                    TextWrapping = TextWrapping.NoWrap,
                    Foreground = EditorBox.Foreground,
                    FontWeight = heading ? FontWeights.Bold : FontWeights.Normal,
                    Opacity = heading ? 0.9 : 0.6,
                    IsHitTestVisible = false
                };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(glyph, Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);
                Canvas.SetTop(glyph, line / (double)map.LineCount * miniatureExtent);
                MinimapCanvas.Children.Add(glyph);
            }
        }
        UpdateMinimapViewport();
    }

    private void UpdateMinimapViewport()
    {
        if (MinimapViewport is null || miniatureExtent <= 0) return;
        var scroller = ViewModel.CurrentNote?.ViewMode == EditorViewMode.Preview ? PreviewScroller : editorScroller;
        if (scroller is null) return;
        var extent = Math.Max(1, scroller.ExtentHeight);
        var viewport = Math.Clamp(scroller.ViewportHeight / extent * miniatureExtent, Math.Min(10, miniatureExtent), miniatureExtent);
        MinimapViewport.Height = viewport;
        MinimapViewport.Margin = new Thickness(0, Math.Clamp(scroller.VerticalOffset / extent * miniatureExtent, 0, miniatureExtent - viewport), 0, 0);
        MinimapViewport.Visibility = Visibility.Visible;
    }

    private void OnMinimapSizeChanged(object sender, SizeChangedEventArgs e) => RebuildMinimap();

    private void NavigateMinimap(PointerRoutedEventArgs e)
    {
        if (ViewModel.CurrentNote is not { } tab || miniatureExtent <= 0) return;
        var ratio = Math.Clamp(e.GetCurrentPoint(MinimapCanvas).Position.Y / miniatureExtent, 0, 1);
        if (tab.ViewMode == EditorViewMode.Preview)
            PreviewScroller.ChangeView(null, ratio * PreviewScroller.ScrollableHeight, null, true);
        else
        {
            var map = tab.Metrics.Lines;
            ViewModel.GoToSourceLine((int)(ratio * Math.Max(0, map.LineCount - 1)));
            editorScroller?.ChangeView(null, ratio * editorScroller.ScrollableHeight, null, true);
        }
        UpdateMinimapViewport();
        e.Handled = true;
    }

    private void OnMinimapPressed(object sender, PointerRoutedEventArgs e)
    {
        draggingMinimap = MinimapCanvas.CapturePointer(e.Pointer);
        NavigateMinimap(e);
    }

    private void OnMinimapMoved(object sender, PointerRoutedEventArgs e)
    {
        if (draggingMinimap) NavigateMinimap(e);
    }

    private void OnMinimapReleased(object sender, PointerRoutedEventArgs e)
    {
        draggingMinimap = false;
        MinimapCanvas.ReleasePointerCapture(e.Pointer);
    }

    private void OnStatusPathTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        => _ = ViewModel.RenameAsync();
    private void OnStatusLineTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        => _ = ViewModel.GoToLineCommand.ExecuteAsync(null);
    private void OnRetrySave(object sender, RoutedEventArgs e)
        => _ = ViewModel.FlushAsync();

    private void OnStatusIssuesTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        => ViewModel.GoToNextIssue();

    private void OnEditorWheel(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(EditorBox);
        if (!point.Properties.IsHorizontalMouseWheel &&
            Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            AdjustEditorFont(point.Properties.MouseWheelDelta > 0 ? 1 : -1);
            e.Handled = true;
        }
    }

    private void AdjustEditorFont(int delta)
    {
        var size = Math.Clamp(SettingsService.Instance.Editor.FontSize + delta, 10, 32);
        _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.FontSize = size);
    }

    private void OnEditorSplitterPressed(object sender, PointerRoutedEventArgs e)
        => BeginSplit(
            EditorPreviewHost,
            0,
            2,
            e,
            (UIElement)sender,
            ViewModel.CurrentNote?.ViewMode == EditorViewMode.SplitVertical);

    private void OnPreviewSplitterPressed(object sender, PointerRoutedEventArgs e)
        => BeginSplit(WorkGrid, 0, 2, e, (UIElement)sender, false);

    private void BeginSplit(Grid host, int left, int right, PointerRoutedEventArgs e, UIElement source, bool rows)
    {
        isSplitting = true;
        splitGrid = host;
        splitUsesRows = rows;
        splitLeft = left;
        splitRight = right;
        lastSplitX = rows
            ? e.GetCurrentPoint(host).Position.Y
            : e.GetCurrentPoint(host).Position.X;
        source.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnSplitterMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isSplitting || splitGrid is null)
        {
            return;
        }

        var position = splitUsesRows
            ? e.GetCurrentPoint(splitGrid).Position.Y
            : e.GetCurrentPoint(splitGrid).Position.X;
        var delta = position - lastSplitX;
        lastSplitX = position;
        if (splitUsesRows)
        {
            var left = splitGrid.RowDefinitions[splitLeft];
            var right = splitGrid.RowDefinitions[splitRight];
            var leftSize = Math.Max(80, left.ActualHeight + delta);
            var rightSize = Math.Max(80, right.ActualHeight - delta);
            left.Height = new GridLength(leftSize);
            right.Height = new GridLength(rightSize);
        }
        else
        {
            var left = splitGrid.ColumnDefinitions[splitLeft];
            var right = splitGrid.ColumnDefinitions[splitRight];
            var leftWidth = Math.Max(left.MinWidth, left.ActualWidth + delta);
            var rightWidth = Math.Max(right.MinWidth, right.ActualWidth - delta);
            left.Width = new GridLength(leftWidth);
            right.Width = new GridLength(rightWidth);
        }

        e.Handled = true;
    }

    private void OnSplitterReleased(object sender, PointerRoutedEventArgs e)
    {
        EndSplit();
        if (sender is UIElement element)
        {
            element.ReleasePointerCaptures();
        }

        e.Handled = true;
    }

    private void OnSplitterCaptureLost(object sender, PointerRoutedEventArgs e) => EndSplit();

    private void EndSplit()
    {
        if (!isSplitting)
        {
            return;
        }

        isSplitting = false;
        splitGrid = null;
        double editorStar;
        var previewStar = 1d;
        if (ViewModel.CurrentNote?.ViewMode == EditorViewMode.SplitVertical)
        {
            var editorHeight = Math.Max(1, EditorRow.ActualHeight);
            var previewHeight = Math.Max(1, PreviewRow.ActualHeight);
            var totalHeight = editorHeight + previewHeight;
            editorStar = Math.Clamp(editorHeight / totalHeight * 2, 0.2, 8);
            previewStar = Math.Clamp(previewHeight / totalHeight * 2, 0.2, 8);
            EditorRow.Height = new GridLength(editorStar, GridUnitType.Star);
            PreviewRow.Height = new GridLength(previewStar, GridUnitType.Star);
        }
        else
        {
            var editorWidth = Math.Max(1, EditorColumn.ActualWidth);
            var previewWidth = Math.Max(1, PreviewColumn.ActualWidth);
            var total = editorWidth + previewWidth;
            editorStar = Math.Clamp(editorWidth / total * 2, 0.2, 8);
            previewStar = Math.Clamp(previewWidth / total * 2, 0.2, 8);
            EditorColumn.Width = new GridLength(editorStar, GridUnitType.Star);
            PreviewColumn.Width = new GridLength(previewStar, GridUnitType.Star);
        }

        var outline = Math.Clamp(OutlineColumn.ActualWidth, 140, 480);
        OutlineColumn.Width = new GridLength(outline);
        _ = SettingsService.Instance.UpdateStateAsync(s =>
        {
            s.Editor.EditorPaneStar = editorStar;
            s.Editor.PreviewPaneStar = previewStar;
            s.Editor.OutlinePaneWidth = outline;
        });
    }
}

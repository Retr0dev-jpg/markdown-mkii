using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Editor;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        WireEditorScroller();
        ApplyEditorSettings();
    }

    private void WireEditorScroller()
    {
        if (editorScroller is not null)
        {
            return;
        }

        editorScroller = VisualTree.FindDescendant<ScrollViewer>(EditorBox);
        if (editorScroller is null)
        {
            return;
        }

        editorScroller.ViewChanged += OnEditorScrollChanged;
    }

    private void OnEditorScrollChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        UpdateMinimapViewport();
        RebuildLineNumbers();

        UpdateCurrentLineHighlight();
        if (!isSyncingScroll && !e.IsIntermediate && SettingsService.Instance.Preview.SyncScroll)
        {
            SyncPreviewToEditor();
        }
    }

    private void OnPreviewScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        UpdateMinimapViewport();
        if (isSyncingScroll || e.IsIntermediate || !SettingsService.Instance.Preview.SyncScroll)
        {
            return;
        }

        SyncEditorToPreview();
    }

    private void SyncEditorToPreview()
    {
        var tab = ViewModel.CurrentNote;
        if (tab is null || PreviewHost.Content is not Panel panel)
        {
            return;
        }

        FrameworkElement? top = null;
        var best = double.MaxValue;
        foreach (var child in panel.Children.OfType<FrameworkElement>())
        {
            Point point;
            try
            {
                point = child.TransformToVisual(PreviewScroller).TransformPoint(new Point(0, 0));
            }
            catch
            {
                continue;
            }

            if (point.Y > 8)
            {
                continue;
            }

            var distance = Math.Abs(point.Y);
            if (distance < best && child.Tag is int)
            {
                best = distance;
                top = child;
            }
        }

        // Lists, quotes and tables are one top-level element; refine to the nested block at the top edge.
        while (top is not null && TaggedDescendants(top)
                   .Select(child => (Element: child, Y: TopOf(child)))
                   .Where(item => item.Y is <= 8)
                   .OrderByDescending(item => item.Y)
                   .Select(item => item.Element)
                   .FirstOrDefault() is { } deeper)
        {
            top = deeper;
        }

        if (top?.Tag is not int line)
        {
            return;
        }

        try
        {
            isSyncingScroll = true;
            var offset = tab.Parsed?.Lines.OffsetOfLine(line) ?? 0;
            EditorBox.Document.GetRange(offset, offset).ScrollIntoView(PointOptions.Start);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Sincronizzazione inversa non riuscita", ex);
        }
        finally
        {
            isSyncingScroll = false;
        }
    }

    private void UpdateCurrentLineHighlight()
    {
        if (ViewModel.CurrentNote is null)
        {
            CurrentLineHighlight.Visibility = Visibility.Collapsed;
            return;
        }

        var showHighlight = SettingsService.Instance.Editor.HighlightCurrentLine;
        var typewriter = SettingsService.Instance.Editor.TypewriterMode;
        if (!showHighlight && !typewriter)
        {
            CurrentLineHighlight.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var (start, _) = GetSelection();
            var map = ViewModel.CurrentNote.Metrics.Lines;
            var line = map.LineOfOffset(start);
            var range = EditorBox.Document.GetRange(map.OffsetOfLine(line), map.OffsetOfLine(line));
            range.GetRect(PointOptions.Transform, out var rect, out _);
            var height = rect.Height > 1 ? rect.Height : EditorBox.FontSize * 1.35;
            CurrentLineHighlight.Height = height;
            CurrentLineHighlight.Margin = new Thickness(0, Math.Max(0, rect.Y), 0, 0);
            CurrentLineHighlight.Visibility = showHighlight ? Visibility.Visible : Visibility.Collapsed;
            if (typewriter && editorScroller is not null && editorScroller.ViewportHeight > 0)
            {
                var delta = rect.Y - (editorScroller.ViewportHeight * 0.42);
                if (Math.Abs(delta) > 8)
                {
                    editorScroller.ChangeView(null, Math.Max(0, editorScroller.VerticalOffset + delta), null, true);
                }
            }
        }
        catch
        {
            CurrentLineHighlight.Visibility = Visibility.Collapsed;
        }
    }

    private void OnGoToLineAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _ = ViewModel.GoToLineCommand.ExecuteAsync(null);
        args.Handled = true;
    }

    private void SyncPreviewToEditor()
    {
        var tab = ViewModel.CurrentNote;
        if (tab?.Preview is null || tab.ViewMode == MarkdownMkII.Core.Models.EditorViewMode.Editor || PreviewScroller.Visibility != Visibility.Visible || PreviewHost.Content is not Panel panel)
        {
            return;
        }

        var line = FirstVisibleLine(tab.Text);
        var sourceLine = ScrollMapper.MapEditorLineToSourceLine(tab.Preview.Blocks, line);
        FrameworkElement? target = null;
        var best = int.MinValue;
        foreach (var child in panel.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is int source && source <= sourceLine && source >= best)
            {
                best = source;
                target = child;
            }
        }

        while (target is not null && TaggedDescendants(target)
                   .Where(child => child.Tag is int source && source <= sourceLine && source >= best)
                   .OrderByDescending(child => (int)child.Tag)
                   .FirstOrDefault() is { } deeper)
        {
            best = (int)deeper.Tag;
            target = deeper;
        }

        if (target is null || PreviewScroller.Content is not UIElement content)
        {
            return;
        }

        try
        {
            isSyncingScroll = true;
            var point = target.TransformToVisual(content).TransformPoint(new Point(0, 0));
            PreviewScroller.ChangeView(null, Math.Max(0, point.Y), null, true);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Sincronizzazione scorrimento non riuscita", ex);
        }
        finally
        {
            isSyncingScroll = false;
        }
    }

    /// <summary>The nearest descendants that carry a source line, without descending past them.</summary>
    private static IEnumerable<FrameworkElement> TaggedDescendants(DependencyObject root)
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Tag: int } tagged)
            {
                yield return tagged;
                continue;
            }

            foreach (var nested in TaggedDescendants(child))
                yield return nested;
        }
    }

    private double? TopOf(FrameworkElement element)
    {
        try { return element.TransformToVisual(PreviewScroller).TransformPoint(new Point(0, 0)).Y; }
        catch { return null; }
    }

    private int FirstVisibleLine(string text)
    {
        var map = ViewModel.CurrentNote?.Metrics.Lines ?? new LineMap(text);
        if (map.LineCount <= 1)
        {
            return 0;
        }

        try
        {
            // Hit-test the visible surface. Querying rectangles halfway through
            // a large wrapped document forces RichEdit to lay out distant text.
            var range = EditorBox.Document.GetRangeFromPoint(
                new Point(EditorBox.Padding.Left + 1, EditorBox.Padding.Top + 1),
                PointOptions.ClientCoordinates | PointOptions.Transform);
            return map.LineOfOffset(Math.Clamp(range.StartPosition, 0, text.Length));
        }
        catch
        {
            var (start, _) = GetSelection();
            return map.LineOfOffset(start);
        }
    }
}

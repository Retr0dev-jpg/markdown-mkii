using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private bool selectionFrameQueued;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? selectionStatsTimer;
    private CancellationTokenSource? selectionStatsRequest;
    private (string Text, int Start, int Length)? statisticsSelection;
    private int currentHeadingLine = -1;

    private void QueueSelectionChrome()
    {
        if (selectionFrameQueued || NoteSecurity.Blocking || !IsLoaded) return;
        selectionFrameQueued = true;
        CompositionTarget.Rendering += OnSelectionFrame;
    }
    private void OnSelectionFrame(object? sender, object e)
    {
        CompositionTarget.Rendering -= OnSelectionFrame;
        selectionFrameQueued = false;
        if (NoteSecurity.Blocking || !IsLoaded || ViewModel.CurrentNote is null) return;
        UpdateStatus(); UpdateCurrentLineHighlight(); UpdateHeadingIndicator();
    }
    private void UpdateHeadingIndicator()
    {
        var tab = ViewModel.CurrentNote;
        if (tab is null) return;
        var line = tab.Metrics.Lines.LineOfOffset(GetSelection().Start);
        var heading = tab.Outline.LastOrDefault(h => h.SourceLine <= line)?.SourceLine ?? -1;
        if (heading == currentHeadingLine) return;
        currentHeadingLine = heading;
        foreach (var item in HeadingRailCanvas.Children.OfType<Border>())
        {
            var active = item.Tag is int source && source == heading;
            item.Background = (Brush)Application.Current.Resources[active ? "AccentFillColorDefaultBrush" : "DividerStrokeColorDefaultBrush"];
            item.Opacity = active ? 0.95 : 0.4;
        }
    }
    private void QueueSelectionStatistics()
    {
        if (ViewModel.CurrentNote is not { } tab || NoteSecurity.Blocking) return;
        var selection = GetSelection();
        var current = (tab.Text, selection.Start, selection.Length);
        if (statisticsSelection is { } previous && ReferenceEquals(previous.Text, tab.Text) && previous.Start == selection.Start && previous.Length == selection.Length) return;
        statisticsSelection = current;
        selectionStatsRequest?.Cancel();
        selectionStatsTimer ??= DispatcherQueue.CreateTimer();
        selectionStatsTimer.IsRepeating = false;
        selectionStatsTimer.Interval = TimeSpan.FromMilliseconds(100);
        selectionStatsTimer.Tick -= OnSelectionStatistics;
        selectionStatsTimer.Tick += OnSelectionStatistics;
        selectionStatsTimer.Stop(); selectionStatsTimer.Start();
    }
    private async void OnSelectionStatistics(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        if (ViewModel.CurrentNote is not { } tab || statisticsSelection is not { } selected || NoteSecurity.Blocking) return;
        selectionStatsRequest?.Dispose();
        var request = selectionStatsRequest = CancellationTokenSource.CreateLinkedTokenSource(NoteSecurity.OperationsToken);
        try
        {
            var stats = await tab.Metrics.SelectionAsync(selected.Start, selected.Length, request.Token);
            if (request.IsCancellationRequested || !IsLoaded || NoteSecurity.Blocking || !ReferenceEquals(tab, ViewModel.CurrentNote) || !ReferenceEquals(tab.Text, selected.Text) || GetSelection() != (selected.Start, selected.Length)) return;
            StatusWords.Text = (selected.Length > 0 ? stats.SelectedWords + "/" : "") + stats.Words + " " + Strings.T("Words");
            StatusChars.Text = (selected.Length > 0 ? stats.SelectedCharacters + "/" : "") + stats.Characters + " " + Strings.T("Characters");
            StatusRead.Text = "~" + Math.Max(1, (int)Math.Ceiling(stats.ReadingTime.TotalMinutes)) + " min" + (tab.IsLargeFile ? " · " + Strings.T("LargeFile") : "");
        }
        catch (OperationCanceledException) { }
    }
    private void ClearSelectionWork()
    {
        CompositionTarget.Rendering -= OnSelectionFrame; selectionFrameQueued = false;
        selectionStatsTimer?.Stop(); selectionStatsRequest?.Cancel();
        statisticsSelection = null; currentHeadingLine = -1;
    }
}

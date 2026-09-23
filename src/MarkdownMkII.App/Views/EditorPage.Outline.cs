using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Preview;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private void OnOutlineClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not OutlineRow row || ViewModel.CurrentNote is null)
        {
            return;
        }

        var control = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (control)
        {
            ViewModel.ToggleFoldHeadingAt(row.Line);
            return;
        }

        // The outline may come from a parse that predates the latest keystrokes.
        var offset = ViewModel.CurrentNote.Metrics.Lines.OffsetOfLine(row.Line);
        SetSelection(offset, 0);
        FocusEditor();
    }

    private async void OnOutgoingClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BacklinkRow row)
        {
            return;
        }

        await OpenLinkedNoteAsync(() => ViewModel.OpenOrCreateMarkdownAsync(row.Path), row.Line);
    }

    private async void OnBacklinkClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BacklinkRow row)
        {
            return;
        }

        await OpenLinkedNoteAsync(() => ViewModel.OpenTargetAsync(row.Path), row.Line);
    }

    private async Task OpenLinkedNoteAsync(Func<Task> open, int line)
    {
        try
        {
            var before = ViewModel.CurrentNote;
            await open();
            // Move the caret only when another note was actually opened.
            if (!ReferenceEquals(before, ViewModel.CurrentNote) && ViewModel.CurrentNote is not null)
                ViewModel.GoToSourceLine(Math.Max(0, line - 1));
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Apertura del collegamento non riuscita", ex);
        }
    }

    private async void OnGraphClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BacklinkRow row)
        {
            return;
        }

        await ViewModel.OpenTargetAsync(row.Path);
    }

    private void BindOutline()
    {
        var tab = ViewModel.CurrentNote;
        if (tab is null)
        {
            OutlineList.ItemsSource = null;
            HeadingRailCanvas?.Children.Clear();
            MinimapCanvas?.Children.Clear();
            return;
        }

        HeadingFold.Prune(tab.FoldedHeadings, tab.Outline, tab.Text);
        var visible = HeadingFold.Visible(tab.Outline, tab.FoldedHeadings);
        var filter = OutlineFilterBox?.Text?.Trim() ?? string.Empty;
        if (filter.Length > 0)
        {
            visible = visible
                .Where(node => node.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        OutlineList.ItemsSource = visible
            .Select(node => new OutlineRow(
                node.Level,
                node.Title,
                node.SourceLine,
                tab.FoldedHeadings.Contains(node.SourceLine),
                HeadingFold.HasBody(node)))
            .ToList();
        RebuildHeadingRail();
        RebuildMinimap();
    }

    private void RebuildHeadingRail()
    {
        if (HeadingRailCanvas is null || isRebuildingChrome)
        {
            return;
        }

        isRebuildingChrome = true;
        try
        {
            HeadingRailCanvas.Children.Clear();
            var tab = ViewModel.CurrentNote;
            if (tab is null || HeadingRailCanvas.ActualHeight <= 1)
            {
                return;
            }

            var map = tab.Metrics.Lines;
            var current = map.LineOfOffset(Math.Clamp(GetSelection().Start, 0, tab.Text.Length));
            var marks = HeadingRail.Plan(tab.Outline, map.LineCount, HeadingRailCanvas.ActualHeight, current);
            var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            var muted = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"];
            foreach (var mark in marks)
            {
                var bar = new Border
                {
                    Width = mark.Level <= 1 ? 10 : 6,
                    Height = Math.Max(3, mark.Height),
                    Background = mark.Current ? accent : muted,
                    Opacity = mark.Current ? 0.95 : 0.4,
                    CornerRadius = new CornerRadius(1),
                    Tag = mark.Line
                };
                ToolTipService.SetToolTip(bar, mark.Title);
                Canvas.SetTop(bar, mark.Top);
                Canvas.SetLeft(bar, mark.Level <= 1 ? 2 : 6);
                HeadingRailCanvas.Children.Add(bar);
            }
        }
        finally
        {
            isRebuildingChrome = false;
        }
    }

    private void OnHeadingRailSizeChanged(object sender, SizeChangedEventArgs e) => RebuildHeadingRail();

    private void OnOutlineFilterChanged(object sender, TextChangedEventArgs e) => BindOutline();

    private void OnHeadingRailPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.CurrentNote is null)
        {
            return;
        }

        if (e.OriginalSource is FrameworkElement { Tag: int line })
        {
            ViewModel.GoToSourceLine(line);
            return;
        }

        var height = HeadingRailCanvas.ActualHeight;
        if (height <= 0)
        {
            return;
        }

        var y = e.GetCurrentPoint(HeadingRailCanvas).Position.Y;
        var count = Math.Max(1, new LineMap(ViewModel.CurrentNote.Text).LineCount);
        var mapped = (int)Math.Clamp(y / height * count, 0, count - 1);
        ViewModel.GoToSourceLine(mapped);
    }

    private void OnOutlineRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement source)
        {
            return;
        }

        OutlineRow? row = null;
        if (OutlineList.SelectedItem is OutlineRow selected)
        {
            row = selected;
        }

        var current = e.OriginalSource as DependencyObject;
        while (current is not null)
        {
            if (current is ListViewItem { Content: OutlineRow found })
            {
                row = found;
                break;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        if (row is null)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var fold = new MenuFlyoutItem { Text = Strings.T("CmdFoldHeading.Label") };
        var line = row.Line;
        fold.Click += (_, _) => ViewModel.ToggleFoldHeadingAt(line);
        flyout.Items.Add(fold);
        var up = new MenuFlyoutItem { Text = Strings.T("CmdMoveHeadingUp.Label") };
        up.Click += (_, _) =>
        {
            var offset = ViewModel.CurrentNote?.Parsed?.Lines.OffsetOfLine(line) ?? 0;
            SetSelection(offset, 0);
            ViewModel.MoveHeadingUp();
        };
        flyout.Items.Add(up);
        var down = new MenuFlyoutItem { Text = Strings.T("CmdMoveHeadingDown.Label") };
        down.Click += (_, _) =>
        {
            var offset = ViewModel.CurrentNote?.Parsed?.Lines.OffsetOfLine(line) ?? 0;
            SetSelection(offset, 0);
            ViewModel.MoveHeadingDown();
        };
        flyout.Items.Add(down);
        var copy = new MenuFlyoutItem { Text = Strings.T("CmdCopyHeading.Label") };
        copy.Click += (_, _) =>
        {
            var offset = ViewModel.CurrentNote?.Parsed?.Lines.OffsetOfLine(line) ?? 0;
            SetSelection(offset, 0);
            ViewModel.CopyHeadingLink();
        };
        flyout.Items.Add(copy);
        flyout.ShowAt(source, e.GetPosition(source));
    }

    private async Task RefreshGraphAsync()
    {
        var version = ++graphRefreshVersion;
        var tab = ViewModel.CurrentNote;
        if (tab?.NoteId is not { } id) { GraphHost.Content = null; GraphList.ItemsSource = null; return; }
        var incoming = await NoteArchive.Database.BacklinksAsync(id, tab.Title);
        var nodes = new Dictionary<string,string> { [id] = tab.Title };
        var edges = new List<(string,string,string?)>();
        foreach (var link in incoming.Take(30)) { nodes[link.Id] = link.Title; edges.Add((link.Id,id,null)); }
        foreach (var link in MarkdownMkII.Storage.NoteDatabase.ExtractLinks(tab.Text).Take(30))
        {
            var matches = await NoteArchive.Database.ResolveAsync(link.Target);
            if (matches.Count != 1) continue;
            nodes[matches[0].Id] = matches[0].Title; edges.Add((id,matches[0].Id,null));
        }
        if (version != graphRefreshVersion || !ReferenceEquals(tab,ViewModel.CurrentNote)) return;
        GraphList.ItemsSource = nodes.Select(n => new BacklinkRow(n.Key,1,n.Value)).ToArray();
        var layout = GraphLayoutEngine.Layered(nodes.Select(n => (n.Key,n.Value)).ToList(),edges,horizontal:true,nodeWidth:110,nodeHeight:28,gap:20);
        GraphHost.Content = PreviewRenderer.RenderGraph(layout,target => _ = ViewModel.OpenNoteAsync(target));
    }
    private void OnLibraryChanged(object? sender, ArchiveChangedEventArgs e)
    {
        if (!IsLoaded || NoteSecurity.Blocking || ViewModel.CurrentNote is null) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!IsLoaded || NoteSecurity.Blocking) return;
            if (BacklinksList.ActualHeight > 0 && BacklinksList.Visibility == Visibility.Visible) _ = RefreshBacklinksAsync();
            if (OutgoingList.ActualHeight > 0 && OutgoingList.Visibility == Visibility.Visible) _ = RefreshOutgoingAsync();
            if (GraphHost.ActualHeight > 0 && GraphHost.Visibility == Visibility.Visible) _ = RefreshGraphAsync();
        });
    }
    private async Task RefreshOutgoingAsync()
    {
        var version = ++outgoingRefreshVersion;
        var tab = ViewModel.CurrentNote;
        if (tab is null) { OutgoingList.ItemsSource = null; return; }
        var rows = new List<BacklinkRow>();
        foreach (var link in MarkdownMkII.Storage.NoteDatabase.ExtractLinks(tab.Text).Take(100))
        {
            var matches = await NoteArchive.Database.ResolveAsync(link.Target);
            if (matches.Count == 1) rows.Add(new(matches[0].Id,1,matches[0].Title));
        }
        if (version == outgoingRefreshVersion && ReferenceEquals(tab,ViewModel.CurrentNote)) OutgoingList.ItemsSource = rows;
    }
    private async Task RefreshBacklinksAsync()
    {
        var version = ++backlinksRefreshVersion;
        var tab = ViewModel.CurrentNote;
        if (tab?.NoteId is not { } id) { BacklinksList.ItemsSource = null; return; }
        var hits = await NoteArchive.Database.BacklinksAsync(id,tab.Title);
        if (version == backlinksRefreshVersion && ReferenceEquals(tab,ViewModel.CurrentNote))
            BacklinksList.ItemsSource = hits.Select(h => new BacklinkRow(h.Id,h.Line+1,h.Title)).ToArray();
    }

    private sealed record OutlineRow(int Level, string Title, int Line, bool Folded, bool HasBody)
    {
        public string Display
        {
            get
            {
                var indent = new string(' ', (Level - 1) * 2);
                var marker = HasBody ? (Folded ? "▸ " : "▾ ") : "  ";
                return indent + marker + Title;
            }
        }

        public override string ToString() => Display;
    }

    private sealed record BacklinkRow(string Path, int Line, string Display)
    {
        public override string ToString() => Display;
    }
}

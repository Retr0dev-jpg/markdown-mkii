using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;
using Windows.System;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.Storage;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace MarkdownMkII.Views;
public sealed partial class NoteBrowser : UserControl
{
    private static readonly (NoteCollection Section, string Key, string Glyph)[] Sections =
    [
        (NoteCollection.All, "ArchiveAll", "\uE8F1"),
        (NoteCollection.Favorites, "ArchiveFavorites", "\uE734"),
        (NoteCollection.Archive, "ArchiveArchived", "\uE7B8"),
        (NoteCollection.Trash, "ArchiveDeleted", "\uE74D")
    ];
    private string? selectionAnchor;
    private bool selecting;
    private bool syncingSelection;
    private readonly HashSet<string> selectedIds = [];
    private NoteCollection section = NoteCollection.All;
    private NoteQuery activeQuery = new();
    private List<string> catalogIds = [];
    private readonly HashSet<string> catalogSet = [];
    private Dictionary<string, int> catalogRanks = [];
    private int catalogRevision;
    private int pageCursor;
    private readonly SemaphoreSlim changes = new(1, 1);
    private bool ready;
    private bool filtering;
    private IncrementalNotes? notes;
    private int generation;
    private int labelGeneration;
    private CancellationTokenSource? request;
    private Button? hoveredMenuButton;
    private bool CanRefresh => ready && App.MainWindow is MainWindow { IsClosed: false } && !NoteSecurity.Blocking;
    public NoteBrowser()
    {
        InitializeComponent();
        foreach (var item in SectionItems())
            item.Text = Strings.T(Sections.First(s => s.Section.ToString() == (string)item.Tag).Key);
        ResetFiltersButton.Content = Strings.T("FiltersReset");
        BulkActionsButton.Content = Strings.T("BulkActions");
        ExitSelectionButton.Content = Strings.T("BulkDone");
        ClearQueryButton.Content = Strings.T("ArchiveClearQuery");
        Label(SelectNotesButton, "BulkSelect"); Label(ExitSelectionButton, "BulkExit");
        Label(SelectAllBox, "BulkSelectAll"); Label(ResetFiltersButton, "FiltersReset");
        NotesList.ContainerContentChanging += (_, args) =>
        {
            if (CardMenuButton(args.ItemContainer?.ContentTemplateRoot) is { } button) button.Opacity = 0;
        };
        UpdateSection();
        UpdateFilterState();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void FocusSearch()
    {
        SearchBox.Focus(FocusState.Keyboard);
        SearchBox.SelectAll();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        NoteArchive.Changed += OnChanged;
        NoteSecurity.Clearing += OnSecurityClearing;
        ready = true;
        await RefreshLabelsAsync();
        await RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ready = false;
        generation++;
        labelGeneration++;
        request?.Cancel();
        notes?.StopAndClear();
        NoteArchive.Changed -= OnChanged;
        NoteSecurity.Clearing -= OnSecurityClearing;
    }

    private void OnSecurityClearing(object? sender, EventArgs e)
    {
        generation++; labelGeneration++; request?.Cancel();
        catalogIds.Clear(); catalogSet.Clear(); catalogRanks.Clear(); SetSelectionMode(false);
        notes?.StopAndClear();
        filtering = true;
        NotesList.ItemsSource = null; CategoryBox.ItemsSource = null; TagBox.ItemsSource = null;
        // A query may name a hidden title; it must not stay visible after the lock.
        SearchBox.Text = "";
        filtering = false;
        UpdateFilterState();
    }
    private void OnChanged(object? sender, ArchiveChangedEventArgs e) => DispatcherQueue.TryEnqueue(async () =>
    {
        await changes.WaitAsync();
        try
        {
            if (!CanRefresh) return;
            if (e.Kind == ArchiveChangeKind.Reset) { await RefreshLabelsAsync(); await RefreshAsync(); return; }
            var beforeLabels = CurrentQuery();
            if (e.LabelsChanged) await RefreshLabelsAsync();
            if (beforeLabels != CurrentQuery()) { await RefreshAsync(); return; }
            if (notes is null) return;
            var version = generation;
            var values = e.Summaries.Count == e.Ids.Count && activeQuery == new NoteQuery()
                ? e.Summaries.Where(n => !n.Trashed && !n.Archived).ToArray()
                : await NoteArchive.Database.SummariesAsync(e.Ids, activeQuery, request?.Token ?? default);
            if (!CanRefresh || generation != version) return;
            catalogRevision++;
            var byId = values.ToDictionary(n => n.Id);
            var cards = notes.ToDictionary(n => n.Note.Id);
            var removed = new HashSet<string>();
            var inserted = new List<NoteSummary>();
            foreach (var id in e.Ids)
            {
                if (byId.TryGetValue(id, out var value))
                {
                    if (cards.TryGetValue(id, out var card)) card.Update(value);
                    else if (!catalogSet.Contains(id)) inserted.Add(value);
                }
                else { removed.Add(id); selectedIds.Remove(id); }
            }
            syncingSelection = true;
            try
            {
                if (removed.Count > 0)
                {
                    pageCursor = catalogIds.Take(pageCursor).Count(id => !removed.Contains(id));
                    catalogIds.RemoveAll(removed.Contains);
                    catalogSet.ExceptWith(removed);
                    foreach (var card in notes.Where(n => removed.Contains(n.Note.Id)).ToArray()) notes.Remove(card);
                }
                if (inserted.Count > 0)
                {
                    catalogIds.InsertRange(0, inserted.Select(n => n.Id));
                    catalogSet.UnionWith(inserted.Select(n => n.Id));
                    // Materialize only a bounded prefix when a bulk operation creates many notes.
                    var added = inserted.Take(100).ToArray();
                    for (var i = added.Length - 1; i >= 0; i--) notes.Insert(0, new(added[i]));
                    if (inserted.Count > 100)
                    {
                        // Keep existing cards and scroll anchors. The remaining
                        // inserted summaries are filled into the gap on demand.
                        pageCursor = 100;
                    }
                    else pageCursor += inserted.Count;
                }
                if (removed.Count > 0 || inserted.Count > 0) RebuildCatalogRanks();
                notes.HasMoreItems = pageCursor < catalogIds.Count;
            }
            finally { syncingSelection = false; }
            ApplySelection();
            UpdateEmptyState(catalogIds.Count);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { DiagnosticsService.LogError("Archive", "Aggiornamento catalogo fallito", ex); }
        finally { changes.Release(); }
    });
    private async Task RefreshLabelsAsync()
    {
        if (!CanRefresh) return;
        var version = ++labelGeneration;
        try
        {
            var session = NoteArchive.Database.SessionVersion;
            var category = (CategoryBox.SelectedItem as NamedLabel)?.Id;
            var tag = (TagBox.SelectedItem as NamedLabel)?.Id;
            var categories = new[]
            {
                new NamedLabel("*", Strings.T("ArchiveAllCategories"), 0),
                new NamedLabel("", Strings.T("ArchiveUncategorized"), 0)
            }.Concat(await NoteArchive.Database.LabelsAsync(true)).ToArray();
            var tags = new[]
            {
                new NamedLabel("*", Strings.T("ArchiveAllTags"), 0)
            }.Concat(await NoteArchive.Database.LabelsAsync(false)).ToArray();
            if (!CanRefresh || version != labelGeneration || session != NoteArchive.Database.SessionVersion) return;
            filtering = true;
            CategoryBox.ItemsSource = categories;
            TagBox.ItemsSource = tags;
            CategoryBox.SelectedItem = categories.FirstOrDefault(c => c.Id == category) ?? categories[0];
            TagBox.SelectedItem = tags.FirstOrDefault(t => t.Id == tag) ?? tags[0];
            UpdateFilterState();
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Lettura etichette fallita", ex);
        }
        finally
        {
            filtering = false;
        }
    }

    private NamedLabel? ActiveCategory => CategoryBox.SelectedItem is NamedLabel { Id: not "*" } category ? category : null;
    private NamedLabel? ActiveTag => TagBox.SelectedItem is NamedLabel { Id: not "*" } tag ? tag : null;
    private NoteQuery CurrentQuery() => new(SearchBox.Text, section, ActiveCategory?.Id, ActiveTag?.Name);

    private async Task RefreshAsync(bool debounce = false)
    {
        if (!CanRefresh || filtering) return;
        var version = ++generation;
        request?.Cancel();
        request?.Dispose();
        request = new();
        var token = request.Token;
        EmptyText.Visibility = RetryButton.Visibility = ClearQueryButton.Visibility = Visibility.Collapsed;
        var query = CurrentQuery();
        activeQuery = query;
        var session = NoteArchive.Database.SessionVersion;
        var source = new IncrementalNotes(collection => LoadPageAsync(collection, query, version, session, token));
        try
        {
            if (debounce) await Task.Delay(200, token);
            if (!CanRefresh || version != generation || token.IsCancellationRequested) return;
            var ids = await NoteArchive.Database.QueryIdsAsync(query, token);
            if (!CanRefresh || version != generation || token.IsCancellationRequested) return;
            catalogIds = ids.ToList(); catalogSet.Clear(); catalogSet.UnionWith(ids);
            // A new query keeps only the selected notes that are still among its results.
            selectedIds.IntersectWith(catalogSet);
            if (selectionAnchor is not null && !catalogSet.Contains(selectionAnchor)) selectionAnchor = null;
            RebuildCatalogRanks(); pageCursor = 0;
            await source.LoadMoreItemsAsync(100);
            if (!CanRefresh || version != generation || token.IsCancellationRequested) return;
            notes?.StopAndClear(); notes = source;
            NotesList.ItemsSource = source;
            ApplySelection();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Lettura catalogo fallita", ex);
            if (version == generation) ShowLoadError();
        }
    }

    private void RebuildCatalogRanks() => catalogRanks = catalogIds.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);

    private async Task<uint> LoadPageAsync(IncrementalNotes source, NoteQuery query, int version, long session, CancellationToken token)
    {
        bool IsCurrent() => CanRefresh && version == generation && !token.IsCancellationRequested
            && session == NoteArchive.Database.SessionVersion;
        if (!IsCurrent()) { source.HasMoreItems = false; return 0; }
        LoadingIndicator.Visibility = Visibility.Visible;
        try
        {
            while (IsCurrent())
            {
                var revision = catalogRevision;
                var loaded = source.Select(card => card.Note.Id).ToHashSet();
                var cursor = pageCursor;
                var ids = new List<string>(100);
                while (cursor < catalogIds.Count && ids.Count < 100)
                {
                    var id = catalogIds[cursor++];
                    if (!loaded.Contains(id)) ids.Add(id);
                }
                var page = await NoteArchive.Database.SummariesAsync(ids, query, token);
                if (!IsCurrent()) { source.HasMoreItems = false; return 0; }
                // A mutation may have removed or moved these targets while the
                // read was running. Discard that snapshot instead of reviving it.
                if (revision != catalogRevision) continue;
                pageCursor = cursor;
                source.HasMoreItems = pageCursor < catalogIds.Count;
                foreach (var note in page)
                {
                    if (!loaded.Add(note.Id) || !catalogRanks.TryGetValue(note.Id, out var rank)) continue;
                    var lo = 0; var hi = source.Count;
                    while (lo < hi)
                    {
                        var mid = (lo + hi) / 2;
                        if (catalogRanks[source[mid].Note.Id] < rank) lo = mid + 1; else hi = mid;
                    }
                    source.Insert(lo, new ArchiveNoteCard(note));
                }
                ApplySelection();
                UpdateEmptyState(source.Count);
                if (page.Count > 0 || !source.HasMoreItems) return (uint)page.Count;
            }
            source.HasMoreItems = false;
            return 0;
        }
        catch (OperationCanceledException) { source.HasMoreItems = false; return 0; }
        catch (Exception ex)
        {
            // Stop automatic retries; a failed page must not create a request/error loop.
            source.HasMoreItems = false;
            DiagnosticsService.LogError("Archive", "Lettura catalogo fallita", ex);
            if (IsCurrent()) ShowLoadError();
            return 0;
        }
        finally { if (IsCurrent()) LoadingIndicator.Visibility = Visibility.Collapsed; }
    }

    private void UpdateEmptyState(int count)
    {
        RetryButton.Visibility = Visibility.Collapsed;
        var narrowed = activeQuery.Search.Trim().Length > 0 || activeQuery.CategoryId is not null || activeQuery.Tag is not null;
        if (count == 0)
            EmptyText.Text = narrowed ? Strings.T("ArchiveNoResults") : Strings.T(activeQuery.Collection switch
            {
                NoteCollection.Favorites => "ArchiveFavoritesEmpty",
                NoteCollection.Archive => "ArchiveArchiveEmpty",
                NoteCollection.Trash => "ArchiveTrashEmpty",
                _ => "ArchiveEmpty"
            });
        EmptyText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearQueryButton.Visibility = count == 0 && narrowed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowLoadError()
    {
        EmptyText.Text = Strings.T("ArchiveOpenFailed");
        EmptyText.Visibility = RetryButton.Visibility = Visibility.Visible;
        ClearQueryButton.Visibility = Visibility.Collapsed;
    }

    private sealed partial class IncrementalNotes(Func<IncrementalNotes, Task<uint>> load)
        : ObservableCollection<ArchiveNoteCard>, ISupportIncrementalLoading
    {
        private bool loading;
        public bool HasMoreItems { get; set; } = true;
        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count) => AsyncInfo.Run(async token =>
        {
            if (loading || !HasMoreItems || token.IsCancellationRequested) return new LoadMoreItemsResult();
            loading = true;
            try { return new LoadMoreItemsResult { Count = await load(this) }; }
            finally { loading = false; }
        });
        public void StopAndClear() { HasMoreItems = false; Clear(); }
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        await RefreshAsync(true);
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && SearchBox.Text.Length > 0) { SearchBox.Text = ""; e.Handled = true; }
        else if (e.Key == VirtualKey.Down && NotesList.Items.Count > 0) { NotesList.Focus(FocusState.Keyboard); e.Handled = true; }
    }

    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFilterState();
        await RefreshAsync();
    }

    private async void OnSectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem { Tag: string tag } || !Enum.TryParse<NoteCollection>(tag, out var value)) return;
        var changed = value != section;
        section = value;
        UpdateSection();
        if (changed) await RefreshAsync();
    }

    private async void OnRetry(object sender, RoutedEventArgs e)
    {
        await RefreshLabelsAsync();
        await RefreshAsync();
    }

    private async void OnNew(SplitButton sender, SplitButtonClickEventArgs e) => await ArchiveDialogs.RunAsync(() => ((MainWindow)App.MainWindow!).Editor.NewDocumentAsync());
    private async void OnImportFiles(object sender, RoutedEventArgs e) => await ArchiveDialogs.RunAsync(() => ArchiveDialogs.ImportAsync());
    private async void OnImportFolder(object sender, RoutedEventArgs e) => await ArchiveDialogs.RunAsync(ArchiveDialogs.ImportFolderAsync);
    private async void OnExportAll(object sender, RoutedEventArgs e) => await ArchiveDialogs.RunAsync(ArchiveDialogs.ExportCollectionAsync);
    private async void OnNoteClick(object sender, ItemClickEventArgs e)
    {
        if (selecting || e.ClickedItem is not ArchiveNoteCard card) return;
        if (card.Note.Trashed)
        {
            // Trashed notes cannot be opened; offer restore and permanent deletion instead.
            if (NotesList.ContainerFromItem(card) is FrameworkElement target) ArchiveDialogs.NoteMenu(card.Note).ShowAt(target);
            return;
        }
        await ((MainWindow)App.MainWindow!).Editor.OpenNoteAsync(card.Note.Id);
    }

    private async void OnNoteContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: NoteSummary note } target) return;
        e.Handled = true;
        var position = e.TryGetPosition(target, out var point) ? point : (Windows.Foundation.Point?)null;
        var menu = await ContextMenuAsync(note);
        if (!CanRefresh) return;
        if (position is { } p) menu.ShowAt(target, p); else menu.ShowAt(target);
    }
    private async void OnNoteMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: NoteSummary note } target) return;
        var menu = await ContextMenuAsync(note);
        if (CanRefresh) menu.ShowAt(target);
    }
    private async Task<MenuFlyout> ContextMenuAsync(NoteSummary note)
    {
        if (!selecting) return ArchiveDialogs.NoteMenu(note);
        if (!selectedIds.Contains(note.Id)) { selectedIds.Clear(); selectedIds.Add(note.Id); ApplySelection(); }
        return await CreateBulkMenuAsync();
    }
    private static Button? CardMenuButton(object? card) => (card as Panel)?.Children.OfType<Button>().FirstOrDefault();
    private void OnCardPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        hoveredMenuButton = CardMenuButton(sender);
        if (hoveredMenuButton is not null) hoveredMenuButton.Opacity = 1;
    }
    private void OnCardPointerExited(object sender, PointerRoutedEventArgs e)
    {
        var button = CardMenuButton(sender);
        if (button == hoveredMenuButton) hoveredMenuButton = null;
        if (button is not null && button.FocusState != FocusState.Keyboard) button.Opacity = 0;
    }
    private void OnCardMenuFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.FocusState == FocusState.Keyboard) button.Opacity = 1;
        else if (button.FocusState == FocusState.Unfocused && button != hoveredMenuButton) button.Opacity = 0;
    }
    private static void Label(FrameworkElement element, string key)
    {
        var text = Strings.T(key); ToolTipService.SetToolTip(element, text); AutomationProperties.SetName(element, text);
    }
    private IEnumerable<RadioMenuFlyoutItem> SectionItems() => [SectionAllItem, SectionFavoritesItem, SectionArchiveItem, SectionTrashItem];
    private void UpdateSection()
    {
        var current = Sections.First(s => s.Section == section);
        SectionText.Text = Strings.T(current.Key);
        SectionIcon.Glyph = current.Glyph;
        ToolTipService.SetToolTip(SectionButton, Strings.T("ArchiveSection"));
        AutomationProperties.SetName(SectionButton, Strings.T("ArchiveSection") + ": " + SectionText.Text);
        foreach (var item in SectionItems()) item.IsChecked = (string)item.Tag == section.ToString();
    }
    private void UpdateFilterState()
    {
        var category = ActiveCategory;
        var tag = ActiveTag;
        var count = (category is null ? 0 : 1) + (tag is null ? 0 : 1);
        FilterBadge.Value = count;
        FilterBadge.Visibility = FilterChips.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        CategoryChip.Visibility = category is null ? Visibility.Collapsed : Visibility.Visible;
        TagChip.Visibility = tag is null ? Visibility.Collapsed : Visibility.Visible;
        CategoryChipText.Text = category?.Name ?? "";
        TagChipText.Text = tag is null ? "" : "#" + tag.Name;
        var remove = Strings.T("FiltersRemove");
        ToolTipService.SetToolTip(CategoryChip, remove + ": " + CategoryChipText.Text); AutomationProperties.SetName(CategoryChip, remove + ": " + CategoryChipText.Text);
        ToolTipService.SetToolTip(TagChip, remove + ": " + TagChipText.Text); AutomationProperties.SetName(TagChip, remove + ": " + TagChipText.Text);
        ResetFiltersButton.IsEnabled = count > 0;
        var labels = new[] { category?.Name, tag is null ? null : TagChipText.Text }.OfType<string>();
        var text = count == 0 ? Strings.T("Filters") : Strings.T("FiltersActive") + ": " + string.Join(" · ", labels);
        ToolTipService.SetToolTip(FilterButton, text); AutomationProperties.SetName(FilterButton, text);
    }
    private void ResetFilters()
    {
        filtering = true;
        if (CategoryBox.Items.Count > 0) CategoryBox.SelectedIndex = 0;
        if (TagBox.Items.Count > 0) TagBox.SelectedIndex = 0;
        filtering = false;
        UpdateFilterState();
    }
    private async void OnResetFilters(object sender, RoutedEventArgs e)
    {
        ResetFilters();
        FilterButton.Flyout?.Hide();
        await RefreshAsync();
    }
    private void OnClearCategory(object sender, RoutedEventArgs e) { if (CategoryBox.Items.Count > 0) CategoryBox.SelectedIndex = 0; }
    private void OnClearTag(object sender, RoutedEventArgs e) { if (TagBox.Items.Count > 0) TagBox.SelectedIndex = 0; }
    private async void OnClearQuery(object sender, RoutedEventArgs e)
    {
        ResetFilters();
        if (SearchBox.Text.Length > 0) SearchBox.Text = "";
        else await RefreshAsync();
    }
    private void OnSelectMode(object sender, RoutedEventArgs e) => SetSelectionMode(true);
    private void OnExitSelection(object sender, RoutedEventArgs e) => SetSelectionMode(false);
    private void SetSelectionMode(bool value)
    {
        var moveFocus = XamlRoot is { } root && FocusManager.GetFocusedElement(root) is DependencyObject focused
            && IsWithin(focused, value ? ToolbarRow : SelectionBar);
        selecting = value;
        syncingSelection = true;
        NotesList.SelectionMode = value ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single;
        NotesList.IsItemClickEnabled = !value;
        selectedIds.Clear(); selectionAnchor = null;
        SelectionBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        ToolbarRow.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        syncingSelection = false; ApplySelection();
        if (moveFocus) (value ? (Control)SelectAllBox : SelectNotesButton).Focus(FocusState.Programmatic);
    }
    private static bool IsWithin(DependencyObject element, DependencyObject container)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current == container) return true;
        return false;
    }
    private void ApplySelection()
    {
        syncingSelection = true;
        try
        {
            if (selecting)
            {
                var selectedCards = NotesList.SelectedItems.Cast<ArchiveNoteCard>().ToHashSet();
                foreach (var card in NotesList.SelectedItems.Cast<ArchiveNoteCard>().ToArray())
                    if (!selectedIds.Contains(card.Note.Id)) NotesList.SelectedItems.Remove(card);
                foreach (var card in (IEnumerable<ArchiveNoteCard>?)notes ?? [])
                    if (selectedIds.Contains(card.Note.Id) && !selectedCards.Contains(card)) NotesList.SelectedItems.Add(card);
            }
            else NotesList.SelectedItem = notes?.FirstOrDefault(n => n.Note.Id == ((MainWindow)App.MainWindow!).Editor.CurrentNote?.NoteId);
        }
        finally { syncingSelection = false; UpdateSelectionCount(); }
    }
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!selecting || syncingSelection) return;
        foreach (var card in e.RemovedItems.OfType<ArchiveNoteCard>()) selectedIds.Remove(card.Note.Id);
        var added = e.AddedItems.OfType<ArchiveNoteCard>().LastOrDefault();
        foreach (var card in e.AddedItems.OfType<ArchiveNoteCard>()) selectedIds.Add(card.Note.Id);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (added is not null && shift && selectionAnchor is not null)
        {
            var start = catalogIds.IndexOf(selectionAnchor); var end = catalogIds.IndexOf(added.Note.Id);
            if (start >= 0 && end >= 0) selectedIds.UnionWith(catalogIds.Skip(Math.Min(start, end)).Take(Math.Abs(end - start) + 1));
            ApplySelection();
        }
        else if (added is not null) selectionAnchor = added.Note.Id;
        UpdateSelectionCount();
    }
    private void UpdateSelectionCount()
    {
        var count = selectedIds.Count;
        SelectionCount.Text = Strings.Format(Strings.DefaultMap, "BulkSelected", count);
        SelectAllBox.IsChecked = count == 0 ? false : count >= catalogIds.Count ? true : null;
        BulkActionsButton.IsEnabled = count > 0;
    }
    private void SelectAllResults() { selectedIds.UnionWith(catalogIds); ApplySelection(); }
    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        if (SelectAllBox.IsChecked == true) SelectAllResults();
        else { selectedIds.Clear(); selectionAnchor = null; ApplySelection(); }
    }
    private void OnBrowserKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Flyouts consume their first Escape; the next one exits selection even
        // when focus has returned to the search field or the actions button.
        if (selecting && !e.Handled && e.Key == VirtualKey.Escape)
        { SetSelectionMode(false); e.Handled = true; }
    }
    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!selecting || e.OriginalSource is TextBox or PasswordBox) return;
        if (e.Key == VirtualKey.Escape) { SetSelectionMode(false); e.Handled = true; }
        else if (e.Key == VirtualKey.A && Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        { SelectAllResults(); e.Handled = true; }
    }
    private async void OnBulkActions(object sender, RoutedEventArgs e)
    {
        var menu = await CreateBulkMenuAsync();
        if (CanRefresh && menu.Items.Count > 0) menu.ShowAt(BulkActionsButton);
    }
    private async Task<MenuFlyout> CreateBulkMenuAsync()
    {
        var ids = selectedIds.ToArray();
        var menu = new MenuFlyout();
        if (ids.Length == 0) return menu;
        try
        {
            var session = NoteArchive.Database.SessionVersion;
            var counts = await BulkNoteActions.CountsAsync(ids, NoteSecurity.OperationsToken);
            if (!CanRefresh || session != NoteArchive.Database.SessionVersion) return menu;
            foreach (var (action, count) in counts)
            {
                if (count == 0) continue;
                var item = new MenuFlyoutItem { Text = Strings.T("Bulk" + action) + " (" + count + ")" };
                item.Click += async (_, _) => await BulkNoteActions.RunAsync(ids, action);
                menu.Items.Add(item);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { DiagnosticsService.LogError("Archive", "Menu multiplo non disponibile", ex); }
        return menu;
    }
}

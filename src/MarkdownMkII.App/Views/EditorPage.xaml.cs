using MarkdownMkII.Core.Models;
using MarkdownMkII.Editor;
using MarkdownMkII.Preview;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Printing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Printing;
using Windows.System;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage : Page, IEditorHost
{
    private const VirtualKey OemPlus = (VirtualKey)187;
    private const VirtualKey OemMinus = (VirtualKey)189;
    private const VirtualKey OemQuestion = (VirtualKey)191;
    private const VirtualKey OemOpenBrackets = (VirtualKey)219;
    private const VirtualKey OemCloseBrackets = (VirtualKey)221;

    private readonly MarkdownHighlighter highlighter = new();
    private readonly PreviewRenderer previewRenderer = new();
    private readonly DispatcherQueueTimer parseTimer;
    private readonly DispatcherQueueTimer highlightTimer;
    private readonly DispatcherQueueTimer chromeHideTimer;
    private string? minimapText;
    private double minimapHeight;
    private double minimapWidth;
    private double miniatureExtent;
    private bool draggingMinimap;
    private bool isApplyingText;
    private bool composing;
    private bool isSyncingScroll;
    private PrintDocument? printDocument;
    private PrintManager? printManager;
    private NoteEditorViewModel? boundTab;
    private bool printWired;
    private ScrollViewer? editorScroller;
    private IReadOnlyList<UIElement> printPages = [];
    private bool isRefreshingPreview;
    private bool isRebuildingChrome;
    private bool isSplitting;
    private int splitLeft;
    private int splitRight;
    private double lastSplitX;
    private Grid? splitGrid;
    private bool splitUsesRows;
    private bool immersive;
    private bool pointerOverChrome;
    private bool commandBarOpen;
    private bool chromeVisible = true;
    private DataTransferManager? shareManager;
    private bool shareWired;
    private int findRangeStart;
    private int findRangeLength = -1;
    private int graphRefreshVersion;
    private int outgoingRefreshVersion;
    private int backlinksRefreshVersion;

    public EditorViewModel ViewModel { get; }

    public EditorPage()
    {
        InitializeComponent();
        ViewModel = App.MainWindow is MainWindow window ? window.Editor : new EditorViewModel();
        ViewModel.AttachHost(this);

        parseTimer = DispatcherQueue.CreateTimer();
        parseTimer.IsRepeating = false;
        parseTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, SettingsService.Instance.Preview.LivePreviewDelayMs));
        parseTimer.Tick += (_, _) => RefreshPreview();

        highlightTimer = DispatcherQueue.CreateTimer();
        highlightTimer.IsRepeating = false;
        highlightTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, SettingsService.Instance.Editor.HighlightDebounceMs));
        highlightTimer.Tick += (_, _) => ApplyHighlight();


        chromeHideTimer = DispatcherQueue.CreateTimer();
        chromeHideTimer.IsRepeating = false;
        chromeHideTimer.Interval = TimeSpan.FromSeconds(3);
        chromeHideTimer.Tick += (_, _) =>
        {
            if (immersive && !pointerOverChrome && !commandBarOpen)
            {
                SetEditorChromeVisible(false);
            }
        };

        EditorBox.SizeChanged += (_, _) => RebuildLineNumbers();
        EditorBox.TextCompositionStarted += (_, _) => composing = true;
        EditorBox.TextCompositionEnded += (_, _) =>
        {
            composing = false;
            OnEditorTextChanged(EditorBox, new RoutedEventArgs());
        };
        InitializeEditorMenus();

        ApplyEditorSettings();
        ApplyPreviewZoom();
        EditorBox.ActualThemeChanged += (_, _) => { ApplyHighlight(); minimapText = null; RebuildMinimap(); };
        FindBox.TextChanged += (_, _) => UpdateFindCount();
        MatchCaseBox.Click += (_, _) => UpdateFindCount();
        WholeWordBox.Click += (_, _) => UpdateFindCount();
        RegexBox.Click += (_, _) => UpdateFindCount();
        Loaded += OnSubscriptionsLoaded;
        Unloaded += OnSubscriptionsUnloaded;
        Loaded += OnPageLoaded;
    }

    private void OnSubscriptionsLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SettingsService.Instance.Changed -= OnSettingsChanged;
        SettingsService.Instance.Changed += OnSettingsChanged;
        NoteArchive.Changed -= OnLibraryChanged; NoteArchive.Changed += OnLibraryChanged;
        NoteSecurity.Clearing -= OnSecurityClearing; NoteSecurity.Clearing += OnSecurityClearing;
        NoteSecurity.Changed -= OnSecurityChanged; NoteSecurity.Changed += OnSecurityChanged;
        ViewModel.AttachHost(this); WireEditorScroller();
        Visibility = Visibility.Visible;
        EditorBox.IsReadOnly = false; LoadCurrentNote();
    }
    private void OnSubscriptionsUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        SettingsService.Instance.Changed -= OnSettingsChanged;
        NoteArchive.Changed -= OnLibraryChanged;
        NoteSecurity.Clearing -= OnSecurityClearing; NoteSecurity.Changed -= OnSecurityChanged;
        if (boundTab is not null) boundTab.PropertyChanged -= OnTabPropertyChanged;
        boundTab = null;
        if (editorScroller is not null) editorScroller.ViewChanged -= OnEditorScrollChanged;
        editorScroller = null;
        parseTimer.Stop(); highlightTimer.Stop(); chromeHideTimer.Stop(); ClearSelectionWork();
        graphRefreshVersion++; outgoingRefreshVersion++; backlinksRefreshVersion++;
        UnwireInterop();
        composing = false;
    }
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (e.PropertyName == nameof(EditorViewModel.SaveStatus)) UpdateStatus();
        if (e.PropertyName == nameof(EditorViewModel.CurrentNote)) LoadCurrentNote();
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Loaded can fire again when the page is reattached; wire and initialize it once.
        Loaded -= OnPageLoaded;
        EditorSplitter.PointerEntered += (_, _) =>
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        EditorSplitter.PointerExited += (_, _) => ProtectedCursor = null;
        PreviewSplitter.PointerEntered += (_, _) =>
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        PreviewSplitter.PointerExited += (_, _) => ProtectedCursor = null;
        EditorCommands.PointerEntered += OnChromePointerEntered;
        EditorCommands.PointerExited += OnChromePointerExited;
        StatusBar.PointerEntered += OnChromePointerEntered;
        StatusBar.PointerExited += OnChromePointerExited;

    }

    private void OnChromePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        pointerOverChrome = true;
        chromeHideTimer.Stop();
        SetEditorChromeVisible(true);
    }

    private void OnChromePointerExited(object sender, PointerRoutedEventArgs e)
    {
        pointerOverChrome = false;
        if (immersive)
        {
            chromeHideTimer.Stop();
            chromeHideTimer.Start();
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyEditorSettings();
    }

    public (int Start, int Length) GetSelection()
    {
        var start = EditorBox.Document.Selection.StartPosition;
        var end = EditorBox.Document.Selection.EndPosition;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        return (start, end - start);
    }

    public void SetSelection(int start, int length)
    {
        EditorBox.Document.Selection.SetRange(start, start + length);
        try
        {
            EditorBox.Document.Selection.ScrollIntoView(PointOptions.Start);
        }
        catch
        {
        }
    }

    public void FocusEditor() => EditorBox.Focus(FocusState.Programmatic);

    public void NavigateToEditor()
    {
        if (App.MainWindow is MainWindow window)
        {
            window.NavigateToEditor();
        }
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = Strings.T("Ok"),
            RequestedTheme = ActualTheme,
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    public void SetImmersive(bool value)
    {
        immersive = value;
        if (App.MainWindow is MainWindow window)
        {
            window.SetImmersive(value);
        }

        chromeHideTimer.Stop();
        SetEditorChromeVisible(true);
        if (value)
        {
            chromeHideTimer.Start();
        }
    }

    private void SetEditorChromeVisible(bool visible)
    {
        if (chromeVisible == visible)
        {
            return;
        }

        chromeVisible = visible;
        FadeTo(FloatingToolbar, visible ? 1 : 0);
        FadeTo(StatusBar, visible ? 1 : 0);
    }

    private static void FadeTo(UIElement element, double opacity)
    {
        element.IsHitTestVisible = opacity > 0;
        if (opacity > 0)
        {
            element.Visibility = Visibility.Visible;
        }

        var animation = new DoubleAnimation
        {
            To = opacity,
            Duration = TimeSpan.FromMilliseconds(180)
        };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            if (opacity <= 0)
            {
                element.Visibility = Visibility.Collapsed;
            }
        };
        storyboard.Begin();
    }

    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!immersive)
        {
            return;
        }

        SetEditorChromeVisible(true);
        chromeHideTimer.Stop();
        chromeHideTimer.Start();
    }

    public void ToggleImmersive() => SetImmersive(!immersive);

    public void ShowReplace() { ShowFind(); ReplaceBox.Focus(FocusState.Programmatic); }
    public void RefreshPreviewNow() => RefreshPreview(force: true);
    public void ToggleInspector() => OnToggleInspector(this, new RoutedEventArgs());

    public void ShowFind()
    {
        FindBar.Visibility = Visibility.Visible;
        if (ViewModel.CurrentNote is not null)
        {
            ViewModel.CurrentNote.FindBarOpen = true;
        }

        FindBox.Focus(FocusState.Programmatic);
        UpdateFindCount();
        ApplyHighlight();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.Includes(SettingsArea.Commands)) DispatcherQueue.TryEnqueue(RefreshCommandTooltips);
        if (e.Includes(SettingsArea.Editor) || e.Includes(SettingsArea.Preview))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyEditorSettings();
                ApplyViewMode(ViewModel.CurrentNote?.ViewMode ?? EditorViewMode.Editor);
            });
        }
    }

    private async void OnOpenLibrary(object sender, RoutedEventArgs e) => await ViewModel.CloseEditorAsync();

    private async void OnToggleInspector(object sender, RoutedEventArgs e)
        => await SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.ShowOutline = !s.Editor.ShowOutline);


}

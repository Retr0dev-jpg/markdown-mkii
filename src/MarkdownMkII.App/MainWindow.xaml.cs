using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Services;
using System.ComponentModel;
using Microsoft.UI.Xaml.Input;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Interop;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using MarkdownMkII.Views;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;

namespace MarkdownMkII;

public sealed partial class MainWindow : Window
{
    private const string Stage = "MainWindow";
    private static readonly TimeSpan PlacementSaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherQueueTimer placementTimer;
    private RectInt32? lastNormalBounds;
    private bool isCloseConfirmed;
    private bool isClosing;
    private bool immersive;
    internal bool IsClosed { get; private set; }
    public string NotesAccessibleName => Strings.T("WorkspaceNotes");

    public EditorViewModel Editor { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        // MSIX takes the taskbar icon from the manifest; the unpackaged build needs it set on the window.
        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(icon)) AppWindow.SetIcon(icon);

        var appearance = SettingsService.Instance.Appearance;
        ApplyTheme(appearance.Theme);
        ApplyBackdrop(appearance.Backdrop);

        RestoreWindowPlacement();

        placementTimer = DispatcherQueue.CreateTimer();
        placementTimer.Interval = PlacementSaveDelay;
        placementTimer.IsRepeating = false;
        placementTimer.Tick += (_, _) => SaveWindowPlacement();

        AppWindow.Changed += OnAppWindowChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Closed += OnClosed;
        SettingsService.Instance.Changed += OnSettingsChanged;
        Editor.PropertyChanged += OnEditorChanged;
        RootGrid.KeyDown += OnGlobalKeyDown;
        RootGrid.ActualThemeChanged += (_, _) => ApplyTitleBarColors();
        NoteSecurity.Initialize(this, RootGrid);
    }

    public void NavigateToEditor() => Navigate("Editor");

    public void ShowEmptyWorkspace()
    {
        if (immersive) SetImmersive(false);
        ContentFrame.Content = null;
        EmptyWorkspace.Visibility = Visibility.Visible;
        BackToEditorButton.Visibility = Visibility.Collapsed;
    }

    private void OnGlobalKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled) return;
        var focused = FocusManager.GetFocusedElement(RootGrid.XamlRoot);
        var inEditor = ContentFrame.Content is EditorPage && focused is not TextBox && focused is not AutoSuggestBox;
        if (ShortcutService.TryExecute(e.Key, Editor, inEditor)) e.Handled = true;
    }

    private void Navigate(string tag)
    {
        if (tag == "Editor" && Editor.CurrentNote is null) { ShowEmptyWorkspace(); return; }
        EmptyWorkspace.Visibility = Visibility.Collapsed;
        var pageType = tag switch
        {
            "Settings" => typeof(SettingsPage),
            "Info" => typeof(InfoPage),
            _ => typeof(EditorPage)
        };
        if (ContentFrame.Content?.GetType() != pageType)
        {
            try { ContentFrame.Navigate(pageType); }
            catch (Exception ex) { DiagnosticsService.LogError(Stage, $"Navigazione a '{tag}' non riuscita", ex); }
        }
        BackToEditorButton.Visibility = pageType == typeof(EditorPage) || Editor.CurrentNote is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public void SelectStartPage() => ShowEmptyWorkspace();

    public void FocusNoteSearch()
    {
        if (immersive)
        {
            if (ContentFrame.Content is EditorPage page) page.SetImmersive(false);
            else SetImmersive(false);
        }
        Browser.FocusSearch();
    }

    public void SetImmersive(bool value)
    {
        immersive = value;
        NotesSidebar.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        NotesColumn.Width = value ? new GridLength(0) : new GridLength(SidebarWidth(RootGrid.ActualWidth));
        RootGrid.RowDefinitions[0].Height = new GridLength(value ? 0 : 40);
        AppTitleBar.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        WorkspaceSurface.CornerRadius = new CornerRadius(value ? 0 : 32);
        WorkspaceSurface.BorderThickness = new Thickness(value ? 0 : 1.5);
        try { AppWindow.SetPresenter(value ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped); }
        catch (Exception ex) { DiagnosticsService.LogError(Stage, "Modalità immersiva non applicata", ex); }
    }

    private static double SidebarWidth(double width) => width < 800 ? 210 : Math.Clamp(width * 0.265, 240, 300);

    private void OnShellSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (NotesColumn is not null && !immersive)
            NotesColumn.Width = new GridLength(SidebarWidth(e.NewSize.Width));
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => Navigate("Settings");
    private void OnInfoClick(object sender, RoutedEventArgs e) => Navigate("Info");
    private void OnBackToEditor(object sender, RoutedEventArgs e) => NavigateToEditor();

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.CurrentNote))
            BackToEditorButton.Visibility = Editor.CurrentNote is null || ContentFrame.Content is EditorPage ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (!e.Includes(SettingsArea.Appearance))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            var appearance = SettingsService.Instance.Appearance;
            ApplyTheme(appearance.Theme);
            ApplyBackdrop(appearance.Backdrop);
        });
    }

    private void ApplyTheme(AppTheme theme)
    {
        RootGrid.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        ApplyTitleBarColors();
    }

    private void ApplyTitleBarColors()
    {
        try
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            // Use the native foreground resolved for this window's theme. Let Windows draw states.
            var foreground = (TitleBarText.Foreground as SolidColorBrush)?.Color;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedForegroundColor = foreground;
            titleBar.ButtonHoverBackgroundColor = null;
            titleBar.ButtonPressedBackgroundColor = null;
            titleBar.ButtonInactiveForegroundColor = null;
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, "Colori della barra del titolo non applicati", ex);
        }
    }

    private void ApplyBackdrop(WindowBackdrop backdrop)
    {
        try
        {
            SystemBackdrop = backdrop switch
            {
                WindowBackdrop.Mica => new MicaBackdrop { Kind = MicaKind.Base },
                WindowBackdrop.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                WindowBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => null
            };
            SolidBackgroundLayer.Visibility = backdrop == WindowBackdrop.Solid
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, $"Materiale della finestra '{backdrop}' non applicato", ex);
        }
    }

    private void RestoreWindowPlacement()
    {
        var appearance = SettingsService.Instance.Appearance;
        if (!appearance.RememberWindowPlacement || appearance.Window is not { } placement)
        {
            AppWindow.Resize(new SizeInt32(1120, 720));
            return;
        }

        if (placement.Width <= 0 || placement.Height <= 0)
        {
            AppWindow.Resize(new SizeInt32(1120, 720));
            return;
        }

        var bounds = new RectInt32(placement.X, placement.Y, placement.Width, placement.Height);
        if (!IntersectsAnyDisplay(bounds))
        {
            AppWindow.Resize(new SizeInt32(Math.Clamp(placement.Width, 640, 1920), Math.Clamp(placement.Height, 480, 1080)));
            return;
        }

        try
        {
            AppWindow.MoveAndResize(bounds);
            lastNormalBounds = bounds;
            if (placement.IsMaximized && AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, "Ripristino della posizione della finestra fallito", ex);
        }
    }

    private static bool IntersectsAnyDisplay(RectInt32 bounds)
    {
        try
        {
            return WindowPlacementInterop.IntersectsAnyMonitor(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, "Verifica dei monitor Win32 fallita; ripristino con dimensioni limitate a 640–1920 × 480–1080", ex);
            return false;
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPositionChange && !args.DidSizeChange)
        {
            return;
        }

        if (!SettingsService.Instance.Appearance.RememberWindowPlacement)
        {
            return;
        }

        if (sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            lastNormalBounds = new RectInt32(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
        }

        placementTimer.Stop();
        placementTimer.Start();
    }

    private Task? SaveWindowPlacement()
    {
        if (!SettingsService.Instance.Appearance.RememberWindowPlacement)
        {
            return null;
        }

        var isMaximized = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
        var bounds = lastNormalBounds ?? new RectInt32(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);
        return SettingsService.Instance.UpdateStateAsync(s => s.Appearance.Window = new WindowPlacement
        {
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = isMaximized
        });
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        IsClosed = true;
        placementTimer.Stop();
        NoteSecurity.Dispose();
        SettingsService.Instance.Changed -= OnSettingsChanged;
        Editor.PropertyChanged -= OnEditorChanged;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (isCloseConfirmed)
        {
            return;
        }

        args.Cancel = true;
        if (!isClosing) _ = ConfirmCloseAsync();
    }

    /// <summary>Closes like the title-bar button: unsaved text is saved and notes are locked first.</summary>
    internal void RequestClose()
    {
        if (!isClosing && !isCloseConfirmed) _ = ConfirmCloseAsync();
    }

    private async Task ConfirmCloseAsync()
    {
        isClosing = true;
        try
        {
            // A draft stored on disk survives closing; only an in-memory one requires unlocking first.
            if (Editor.HasVolatileProtectedDraft)
            {
                if (!await NoteSecurity.EnsureUnlockedAsync() || Editor.HasVolatileProtectedDraft) return;
            }
            if (!await Editor.FlushAsync(true)) { NavigateToEditor(); return; }

            placementTimer.Stop();
            await (SaveWindowPlacement() ?? Task.CompletedTask);
            await SettingsService.Instance.FlushAsync();
            // Input can arrive while the final state writes are pending.
            if (Editor.HasUnsavedChanges && !await Editor.FlushAsync(true)) { NavigateToEditor(); return; }
            await NoteSecurity.LockAsync();
            if (NoteSecurity.Blocking || Editor.HasVolatileProtectedDraft || Editor.HasUnsavedChanges) return;
            // Release the editor and move focus off text input while XAML input
            // services are still alive. Focused RichEdit teardown can fault in WinUI.
            await Editor.CloseEditorAsync();
            if (Editor.CurrentNote is not null) return;
            SettingsButton.Focus(FocusState.Programmatic);
            isCloseConfirmed = true;
            AppUpdates.ApplyOnExit();
            DispatcherQueue.TryEnqueue(Close);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, "Conferma chiusura fallita", ex);
        }

        finally
        {
            isClosing = false;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        await ArchiveDialogs.ImportAsync(items.Where(item => item is StorageFolder || DocumentStore.IsMarkdown(item.Path)).Select(item => item.Path));
    }
}

using MarkdownMkII.Services;
using MarkdownMkII.Services.Interop;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Printing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Printing;
using Windows.Storage;
using WinRT.Interop;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private string? shareNoteId;
    private string? printNoteId;
    private long shareSession;
    private long printSession;
    public async void Share()
    {
        if (ViewModel.CurrentNote?.NoteId is not { } id || !await NoteSecurity.AuthorizeExportAsync([id])) return;
        try
        {
            if (App.MainWindow is null || ViewModel.CurrentNote is null)
            {
                return;
            }

            if (ViewModel.CurrentNote?.NoteId != id) return;
            shareNoteId = id; shareSession = NoteArchive.Database.SessionVersion;
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            shareManager ??= ShareInterop.GetForWindow(hwnd);
            if (!shareWired)
            {
                shareManager.DataRequested += OnShareDataRequested;
                shareWired = true;
            }

            ShareInterop.ShowShareUI(hwnd);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Condivisione non disponibile", ex);
        }
    }

    private void OnShareDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        var deferral = args.Request.GetDeferral();
        try
        {
            var tab = ViewModel.CurrentNote;
            if (tab is null || NoteSecurity.Blocking || tab.NoteId != shareNoteId || shareSession != NoteArchive.Database.SessionVersion)
            {
                args.Request.FailWithDisplayText(Strings.T("ShareEmpty"));
                return;
            }

            args.Request.Data.Properties.Title = tab.FileName;
            args.Request.Data.SetText(tab.Text);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Condivisione fallita", ex);
            args.Request.FailWithDisplayText(Strings.T("ShareFailed"));
        }
        finally
        {
            deferral.Complete();
        }
    }

    public async void Print()
    {
        if (ViewModel.CurrentNote?.NoteId is not { } id || !await NoteSecurity.AuthorizeExportAsync([id])) return;
        try
        {
            if (App.MainWindow is null || ViewModel.CurrentNote?.Preview is null)
            {
                return;
            }

            if (ViewModel.CurrentNote?.NoteId != id) return;
            printNoteId = id; printSession = NoteArchive.Database.SessionVersion;
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            printManager ??= PrintManagerInterop.GetForWindow(hwnd);
            printDocument ??= new PrintDocument();
            if (!printWired)
            {
                printManager.PrintTaskRequested += OnPrintTaskRequested;
                printDocument.Paginate += OnPaginate;
                printDocument.GetPreviewPage += OnGetPreviewPage;
                printDocument.AddPages += OnAddPages;
                printWired = true;
            }

            await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Editor", "Stampa non disponibile", ex);
        }
    }

    // Print and share managers belong to the window and outlive this page; a recreated page must not add a second handler.
    private void UnwireInterop()
    {
        if (shareWired && shareManager is not null)
        {
            shareManager.DataRequested -= OnShareDataRequested;
            shareWired = false;
        }

        if (printWired)
        {
            if (printManager is not null) printManager.PrintTaskRequested -= OnPrintTaskRequested;
            if (printDocument is not null)
            {
                printDocument.Paginate -= OnPaginate;
                printDocument.GetPreviewPage -= OnGetPreviewPage;
                printDocument.AddPages -= OnAddPages;
            }

            printWired = false;
        }
    }

    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        args.Request.CreatePrintTask("Markdown MkII", e =>
        {
            e.SetSource(printDocument?.DocumentSource);
        });
    }

    private IReadOnlyList<UIElement> BuildPrintPages()
    {
        if (NoteSecurity.Blocking || ViewModel.CurrentNote?.NoteId != printNoteId || printSession != NoteArchive.Database.SessionVersion || ViewModel.CurrentNote?.Preview is null)
        {
            return [new TextBlock { Text = string.Empty }];
        }

        return previewRenderer.RenderPages(
            ViewModel.CurrentNote.Preview,
            editor: null,
            SettingsService.Instance.Preview.Zoom);
    }

    private void OnPaginate(object sender, PaginateEventArgs e)
    {
        printPages = BuildPrintPages();
        printDocument?.SetPreviewPageCount(Math.Max(1, printPages.Count), PreviewPageCountType.Final);
    }

    private void OnGetPreviewPage(object sender, GetPreviewPageEventArgs e)
    {
        var index = Math.Clamp(e.PageNumber - 1, 0, Math.Max(0, printPages.Count - 1));
        printDocument?.SetPreviewPage(e.PageNumber, printPages.Count == 0 ? new TextBlock() : printPages[index]);
    }

    private void OnAddPages(object sender, AddPagesEventArgs e)
    {
        if (printPages.Count == 0)
        {
            printPages = BuildPrintPages();
        }

        foreach (var page in printPages)
        {
            printDocument?.AddPage(page);
        }

        printDocument?.AddPagesComplete();
    }

    private async Task<bool> ShowDialogAsync(string title, string message, string primary, string close)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = string.IsNullOrWhiteSpace(primary) ? Strings.T("Ok") : primary,
            CloseButtonText = close,
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ActualTheme,
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}

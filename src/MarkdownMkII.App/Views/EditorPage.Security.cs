using MarkdownMkII.Services;
using Microsoft.UI.Text;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private void OnSecurityClearing(object? sender, EventArgs e)
    {
        // Text still only in the control (IME composition, last keystrokes) must reach the model
        // before the story is erased, so it is saved or sealed instead of lost.
        if (boundTab is not null && ReferenceEquals(boundTab, ViewModel.CurrentNote) && IsLoaded)
        {
            try
            {
                EditorBox.Document.GetText(TextGetOptions.None, out var raw);
                var text = ReadEditorText(raw);
                if (text != boundTab.Text)
                {
                    var (start, length) = GetSelection();
                    boundTab.SetTextFromEditor(text, start, length);
                }
            }
            catch (Exception ex) { DiagnosticsService.LogError("Security", "Testo dell'editor non letto prima del blocco", ex); }
        }

        composing = false;
        ClearSelectionWork();
        Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        parseTimer.Stop(); highlightTimer.Stop();
        graphRefreshVersion++; outgoingRefreshVersion++; backlinksRefreshVersion++;
        if (boundTab?.Metadata?.IsProtected != false)
        {
            isApplyingText = true;
            try
            {
                // RichEdit rejects programmatic writes while IsReadOnly is true.
                EditorBox.IsReadOnly = false;
                EditorBox.Document.SetText(TextSetOptions.None, "");
            }
            finally { isApplyingText = false; }
            if (boundTab is not null) boundTab.PropertyChanged -= OnTabPropertyChanged;
            boundTab = null;
        }
        EditorBox.IsReadOnly = true;
        PreviewHost.Content = null;
        GraphHost.Content = null;
        OutlineList.ItemsSource = null; BacklinksList.ItemsSource = null;
        OutgoingList.ItemsSource = null; GraphList.ItemsSource = null;
        TagsLine.Text = ""; FindBox.Text = ""; ReplaceBox.Text = "";
        MinimapCanvas.Children.Clear(); HeadingRailCanvas.Children.Clear(); LineNumbers.Children.Clear();
        minimapText = null; printPages = []; shareNoteId = null; printNoteId = null;
        if (boundTab is not null)
        {
            boundTab.NoteResources = null;
        }
    }

    private void OnSecurityChanged(object? sender, EventArgs e)
    {
        if (NoteSecurity.Blocking || App.MainWindow is not MainWindow { IsClosed: false }) return;
        Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        EditorBox.IsReadOnly = false;
        if (ViewModel.CurrentNote?.Metadata?.IsProtected == true && !NoteArchive.Database.IsUnlocked) return;
        if (!ReferenceEquals(boundTab, ViewModel.CurrentNote)) LoadCurrentNote();
        else
        {
            RefreshPreview(force: true);
            RebuildLineNumbers();
            RebuildMinimap();
        }
    }
}

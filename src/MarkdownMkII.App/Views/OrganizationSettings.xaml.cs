using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownMkII.Views;
public sealed partial class OrganizationSettings : UserControl
{
    private bool Categories => KindBox.SelectedIndex == 0;

    public OrganizationSettings()
    {
        InitializeComponent();
        KindBox.Items.Add(Strings.T("ArchiveCategories"));
        KindBox.Items.Add(Strings.T("ArchiveTags"));
        KindBox.SelectedIndex = 0;
        Loaded += async (_, _) => { NoteSecurity.Clearing += OnSecurityClearing; NoteSecurity.Changed += OnSecurityChanged; await RefreshAsync(); };
        Unloaded += (_, _) => { NoteSecurity.Clearing -= OnSecurityClearing; NoteSecurity.Changed -= OnSecurityChanged; };
    }

    private void OnSecurityClearing(object? sender, EventArgs e) { LabelsList.ItemsSource = null; NameBox.Text = ""; }
    private async void OnSecurityChanged(object? sender, EventArgs e) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        var session = NoteArchive.Database.SessionVersion;
        var labels = await NoteArchive.Database.LabelsAsync(Categories);
        if (LabelsList is not null && session == NoteArchive.Database.SessionVersion && !NoteSecurity.Blocking) LabelsList.ItemsSource = labels;
    }

    private async void OnKindChanged(object sender, SelectionChangedEventArgs e) => await RefreshAsync();
    private async Task ChangeAsync(Func<Task> change, string? labelId = null)
    {
        try
        {
            var ids = labelId is null ? Array.Empty<string>() : await NoteArchive.Database.LabelNoteIdsAsync(Categories, labelId);
            await change();
            NoteArchive.NotifyChanged(ArchiveChangeKind.Organization, ids);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Modifica etichetta fallita", ex);
            await ArchiveDialogs.MessageAsync(Strings.T("ArchiveActionFailed"), Strings.T("OrganizationNameConflict"));
        }
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
            return;
        await ChangeAsync(async () =>
        {
            if (Categories)
                await NoteArchive.Database.AddCategoryAsync(NameBox.Text);
            else
                await NoteArchive.Database.AddTagAsync(NameBox.Text);
            NameBox.Text = "";
        });
    }

    private async void OnRename(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id })
            return;
        if ((await NoteArchive.Database.ProtectionSettingsAsync()).Configured && !await NoteSecurity.EnsureUnlockedAsync()) return;
        var label = (await NoteArchive.Database.LabelsAsync(Categories)).FirstOrDefault(l => l.Id == id);
        if (label is null)
            return;
        var name = await ArchiveDialogs.PromptAsync(Strings.T("RenameTitle"), label.Name);
        if (!string.IsNullOrWhiteSpace(name))
            await ChangeAsync(() => NoteArchive.Database.RenameLabelAsync(Categories, id, name), id);
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if ((await NoteArchive.Database.ProtectionSettingsAsync()).Configured && !await NoteSecurity.EnsureUnlockedAsync()) return;
        if (sender is FrameworkElement { Tag: string id })
            await ChangeAsync(() => NoteArchive.Database.DeleteLabelAsync(Categories, id), id);
    }
}

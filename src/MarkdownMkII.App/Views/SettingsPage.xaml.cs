using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownMkII.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
        CategoryNavigation.SelectedItem = CategoryNavigation.MenuItems[0];
        foreach (var (tag, display) in ViewModel.Languages)
        {
            LanguageBox.Items.Add(new ComboBoxItem { Content = display, Tag = tag });
            if (string.Equals(tag, ViewModel.Language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedIndex = LanguageBox.Items.Count - 1;
            }
        }

        if (LanguageBox.SelectedIndex < 0 && LanguageBox.Items.Count > 0)
        {
            LanguageBox.SelectedIndex = 0;
        }

    }

    private void OnCategorySelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (SettingsSections is null || args.SelectedItem is not NavigationViewItem { Tag: string category })
            return;

        foreach (FrameworkElement section in SettingsSections.Children)
            section.Visibility = Equals(section.Tag, category) ? Visibility.Visible : Visibility.Collapsed;
        SettingsScroller.ChangeView(null, 0, null, true);
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is ComboBoxItem { Tag: string tag })
        {
            ViewModel.Language = tag;
        }
    }

    private async void OnBackup(object sender, RoutedEventArgs e)
    {
        try { await MarkdownMkII.Services.ArchiveBackup.CreateAsync(); }
        catch (Exception ex) { await MarkdownMkII.Services.ArchiveDialogs.MessageAsync(Strings.T("ArchiveActionFailed"), ex.Message); }
    }
    private async void OnRestoreBackup(object sender, RoutedEventArgs e) => await MarkdownMkII.Services.ArchiveBackup.RestoreAsync();
    private async void OnFactoryResetClick(object sender, RoutedEventArgs e) => await MarkdownMkII.Services.ArchiveReset.RunAsync();

    private async void OnResetClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = Strings.T("SettingsReset.Header"),
            Content = Strings.T("SettingsResetConfirm"),
            PrimaryButtonText = Strings.T("SettingsResetButton.Content"),
            CloseButtonText = Strings.T("Cancel"),
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = ActualTheme,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ResetCommand.ExecuteAsync(null);
        }
    }
}

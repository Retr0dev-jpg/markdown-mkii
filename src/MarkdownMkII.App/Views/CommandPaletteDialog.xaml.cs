using MarkdownMkII.Core.Editor;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace MarkdownMkII.Views;
public sealed partial class CommandPaletteDialog : ContentDialog
{
    private readonly EditorViewModel viewModel;
    private PaletteCommand? chosen;
    private static readonly string[] Frequent = ["new", "quickOpen", "find", "bold", "italic", "wiki", "table", "history", "exportMd", "close"];
    public sealed record Group(string Name, IReadOnlyList<PaletteCommand> Items);
    public CommandPaletteDialog(EditorViewModel vm)
    {
        viewModel = vm;
        InitializeComponent();
        Title = Strings.T("ArchiveCommandSearch");
        Opened += (_, _) =>
        {
            Refresh();
            SearchBox.Focus(FocusState.Programmatic);
        };
    }

    public static async Task ShowForAsync(EditorViewModel vm)
    {
        var root = (FrameworkElement)App.MainWindow!.Content;
        var previous = FocusManager.GetFocusedElement(root.XamlRoot) as Control;
        var dialog = new CommandPaletteDialog(vm)
        {
            XamlRoot = root.XamlRoot,
            RequestedTheme = root.ActualTheme
        };
        ((Grid)dialog.Content).Width = Math.Max(280, Math.Min(500, root.ActualWidth - 100));
        await dialog.ShowAsync();
        previous?.Focus(FocusState.Programmatic);
        if (dialog.chosen is { } command)
            await ShortcutService.ExecuteAsync(command);
    }

    private void Refresh()
    {
        if (ResultsList is null)
            return;
        var commands = EditorPalette.Commands(viewModel).Where(c => c.Id != "palette").ToDictionary(c => c.Id);
        var recent = SettingsService.Instance.Current.RecentCommands;
        var ids = CommandSearch.Find(commands.Values.Select(c => new CommandSearchEntry(c.Id, c.Label, c.Category, c.Keywords, (viewModel.CurrentNote is not null || c.IsGlobal) && c.Command.CanExecute(null))), SearchBox.Text, recent, Frequent);
        IEnumerable<Group> groups;
        if (string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            var last = recent.Take(5).ToHashSet();
            groups = new[]
            {
                new Group(Strings.T("CommandRecent"), ids.Where(last.Contains).Select(id => commands[id]).ToArray()),
                new Group(Strings.T("CommandFrequent"), ids.Where(id => !last.Contains(id)).Select(id => commands[id]).ToArray())
            }.Where(g => g.Items.Count > 0);
        }
        else
            groups = [new Group(Strings.T("CommandResults"), ids.Select(id => commands[id]).ToArray())];
        ResultsList.ItemsSource = new CollectionViewSource
        {
            Source = groups.ToArray(),
            IsSourceGrouped = true,
            ItemsPath = new PropertyPath("Items")
        }.View;
        if (ResultsList.Items.Count > 0)
            ResultsList.SelectedIndex = 0;
        HintText.Text = Strings.T(ids.Count == 0 ? "CommandNoResults" : "CommandKeyboardHint");
    }

    private void OnSearch(object sender, TextChangedEventArgs e) => Refresh();
    private void OnCommandClick(object sender, ItemClickEventArgs e)
    {
        chosen = e.ClickedItem as PaletteCommand;
        Hide();
    }

    private void OnSearchKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ResultsList.SelectedItem is PaletteCommand command)
        {
            chosen = command;
            e.Handled = true;
            Hide();
        }
        else if (e.Key is VirtualKey.Down or VirtualKey.Up && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = Math.Clamp(ResultsList.SelectedIndex + (e.Key == VirtualKey.Down ? 1 : -1), 0, ResultsList.Items.Count - 1);
            ResultsList.ScrollIntoView(ResultsList.SelectedItem);
            e.Handled = true;
        }
    }
}

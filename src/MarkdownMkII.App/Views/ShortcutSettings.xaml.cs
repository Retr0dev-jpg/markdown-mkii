using MarkdownMkII.Core.Editor;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace MarkdownMkII.Views;
public sealed partial class ShortcutSettings : UserControl
{
    private IReadOnlyList<PaletteCommand> Commands => EditorPalette.Commands(((MainWindow)App.MainWindow!).Editor);

    public ShortcutSettings()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        if (App.MainWindow is null || CommandsList is null)
            return;
        var query = SearchBox.Text.Trim();
        CommandsList.ItemsSource = Commands.Where(c => (c.Label + " " + c.Category + " " + c.Shortcut).Contains(query, StringComparison.CurrentCultureIgnoreCase)).OrderBy(c => c.Category).ThenBy(c => c.Label).Select(c => c with { }).ToArray();
    }

    private void OnSearch(object sender, TextChangedEventArgs e) => Refresh();
    private async void OnReset(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PaletteCommand command })
            return;
        if (CommandGesture.TryParse(command.DefaultShortcut, out var gesture) && ShortcutService.Conflict(command, gesture, Commands)is { } other)
        {
            await ArchiveDialogs.MessageAsync(command.Label, Strings.Format(Strings.DefaultMap, "ShortcutConflict", other));
            return;
        }

        await ShortcutService.ResetAsync(command.Id);
        Refresh();
    }

    private async void OnResetAll(object sender, RoutedEventArgs e)
    {
        await ShortcutService.ResetAsync();
        Refresh();
    }

    private async void OnEdit(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PaletteCommand command })
            return;
        var input = new TextBox
        {
            IsReadOnly = true,
            PlaceholderText = Strings.T("ShortcutPressKeys"),
            MinWidth = 300
        };
        var message = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 400
        };
        var panel = new StackPanel
        {
            Spacing = 12
        };
        panel.Children.Add(input);
        panel.Children.Add(message);
        var dialog = new ContentDialog
        {
            Title = command.Label,
            Content = panel,
            PrimaryButtonText = Strings.T("Ok"),
            SecondaryButtonText = Strings.T("ShortcutRemove"),
            CloseButtonText = Strings.T("Cancel"),
            IsPrimaryButtonEnabled = false,
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme
        };
        CommandGesture? selected = null;
        input.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Escape)
                return;
            args.Handled = true;
            if (args.Key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu or VirtualKey.LeftControl or VirtualKey.RightControl or VirtualKey.LeftShift or VirtualKey.RightShift or VirtualKey.LeftMenu or VirtualKey.RightMenu)
                return;
            var gesture = ShortcutService.Current(args.Key);
            selected = gesture;
            input.Text = gesture.ToString();
            var conflict = ShortcutService.Conflict(command, gesture, Commands);
            dialog.IsPrimaryButtonEnabled = gesture.CanAssign && conflict is null;
            message.Text = !gesture.CanAssign ? Strings.T("ShortcutReserved") : conflict is not null ? Strings.Format(Strings.DefaultMap, "ShortcutConflict", conflict) : Strings.T("ShortcutReady");
        };
        dialog.Opened += (_, _) => input.Focus(FocusState.Programmatic);
        try
        {
            ShortcutService.Capturing = true;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && selected is { } gesture)
                await ShortcutService.AssignAsync(command.Id, gesture.ToString());
            else if (result == ContentDialogResult.Secondary)
                await ShortcutService.AssignAsync(command.Id, null);
        }
        finally
        {
            ShortcutService.Capturing = false;
            Refresh();
        }
    }
}

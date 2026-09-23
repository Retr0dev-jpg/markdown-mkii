using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Path = System.IO.Path;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    public Task<bool> ConfirmAsync(string title, string message, string primaryButtonText)
        => ShowDialogAsync(title, message, primaryButtonText, Strings.T("Cancel"));

    public async Task<string?> PromptAsync(string title, string? currentValue)
    {
        var box = new TextBox { Text = currentValue ?? string.Empty };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = box,
            PrimaryButtonText = Strings.T("Ok"),
            CloseButtonText = Strings.T("Cancel"),
            RequestedTheme = ActualTheme,
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? box.Text : null;
    }

    public void ShowCommandPalette()
    {
        _ = ShowCommandPaletteAsync();
    }

    public void ShowQuickOpen()
    {
        _ = ShowQuickOpenAsync();
    }

    public void ShowGoToHeading()
    {
        _ = ShowGoToHeadingAsync();
    }

    private Task ShowCommandPaletteAsync() => CommandPaletteDialog.ShowForAsync(ViewModel);

    private async Task ShowQuickOpenAsync()
    {
        var note = await ArchiveDialogs.PickNoteAsync();
        if (note is not null) await ViewModel.OpenNoteAsync(note.Id);
    }

    private async Task ShowGoToHeadingAsync()
    {
        var tab = ViewModel.CurrentNote;
        if (tab is null)
        {
            return;
        }

        var headings = tab.Outline
            .Select(node => new HeadingPick(node.Title, node.Id, node.SourceLine, node.Level))
            .ToList();
        var chosen = await ShowFilterDialogAsync(
            Strings.T("CmdGoToHeading.Label"),
            Strings.T("HeadingPlaceholder.PlaceholderText"),
            headings,
            item => item.Title + " " + item.Id,
            item => new string(' ', Math.Max(0, item.Level - 1) * 2) + item.Title);
        if (chosen is not null)
        {
            ViewModel.GoToSourceLine(chosen.Line);
        }
    }

    private async Task<T?> ShowFilterDialogAsync<T>(
        string title,
        string placeholder,
        IReadOnlyList<T> source,
        Func<T, string> haystack,
        Func<T, string> display,
        Func<string, T?>? typed = null) where T : notnull
    {
        var rows = source.Select(item => new PickerRow(display(item), haystack(item), item)).ToList();
        var box = new TextBox { PlaceholderText = placeholder };
        var list = new ListView
        {
            MaxHeight = 280,
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
            DisplayMemberPath = nameof(PickerRow.Label)
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(box);
        panel.Children.Add(list);

        void Refresh(string query)
        {
            var items = PaletteFilter.Apply(rows, row => row.Haystack, query);
            list.ItemsSource = items;
            if (items.Count > 0)
            {
                list.SelectedIndex = 0;
            }
        }

        box.TextChanged += (_, _) => Refresh(box.Text);
        Refresh(string.Empty);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = Strings.T("Ok"),
            CloseButtonText = Strings.T("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = ActualTheme,
            XamlRoot = XamlRoot
        };

        PickerRow? chosen = null;
        list.ItemClick += (_, args) =>
        {
            chosen = args.ClickedItem as PickerRow;
            dialog.Hide();
        };
        box.KeyDown += (_, args) =>
        {
            if (list.Items.Count == 0)
            {
                return;
            }

            if (args.Key == VirtualKey.Down)
            {
                list.SelectedIndex = Math.Min(list.Items.Count - 1, list.SelectedIndex + 1);
                list.ScrollIntoView(list.SelectedItem);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.Up)
            {
                list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1);
                list.ScrollIntoView(list.SelectedItem);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.Enter)
            {
                chosen = list.SelectedItem as PickerRow;
                dialog.Hide();
                args.Handled = true;
            }
        };

        var result = await dialog.ShowAsync();
        if (chosen is null && result == ContentDialogResult.Primary)
        {
            chosen = list.SelectedItem as PickerRow;
            if (chosen is null && typed is not null)
            {
                var typedValue = typed(box.Text);
                if (typedValue is not null)
                {
                    return typedValue;
                }
            }
        }

        return chosen is null ? default : (T)chosen.Value!;
    }

    public async Task<string?> PickAsync(string title, string placeholder, IReadOnlyList<string> items)
    {
        var chosen = await ShowFilterDialogAsync(
            title,
            placeholder,
            items.Select(item => new QuickOpenRow(item, item, string.Empty)).ToList(),
            item => item.Name,
            item => item.Name,
            text => string.IsNullOrWhiteSpace(text) ? null : new QuickOpenRow(text.Trim(), text.Trim(), string.Empty));
        return chosen?.Name;
    }

    private sealed record PickerRow(string Label, string Haystack, object Value);

    private sealed record QuickOpenRow(string Path, string Name, string Folder);

    private sealed record HeadingPick(string Title, string Id, int Line, int Level);
}

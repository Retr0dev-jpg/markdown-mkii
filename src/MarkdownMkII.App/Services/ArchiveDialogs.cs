using MarkdownMkII.Core;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;

namespace MarkdownMkII.Services;
public static class ArchiveDialogs
{
    private static FrameworkElement Root => (FrameworkElement)App.MainWindow!.Content;

    private static ContentDialog Dialog(string title, object content, string? primary = null) => new()
    {
        Title = title,
        Content = content,
        XamlRoot = Root.XamlRoot,
        RequestedTheme = Root.ActualTheme,
        PrimaryButtonText = primary ?? string.Empty,
        CloseButtonText = Strings.T("Cancel"),
        DefaultButton = ContentDialogButton.None
    };
    public static async Task<string?> PromptAsync(string title, string text = "")
    {
        var box = new TextBox
        {
            Text = text,
            MinWidth = 280
        };
        var dialog = Dialog(title, box, Strings.T("Ok"));
        dialog.Opened += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }

    public static async Task MessageAsync(string title, string message)
    {
        var dialog = Dialog(title, new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 });
        dialog.CloseButtonText = Strings.T("LibraryClose");
        await dialog.ShowAsync();
    }

    public static async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Operazione non riuscita", ex);
            await MessageAsync(Strings.T("ArchiveActionFailed"), ex.Message);
        }
    }

    public static MenuFlyout NoteMenu(NoteSummary note)
    {
        var menu = new MenuFlyout();
        void Add(string key, Func<Task> action, string glyph)
        {
            var item = new MenuFlyoutItem
            {
                Text = Strings.T(key),
                Icon = new FontIcon { Glyph = glyph }
            };
            item.Click += async (_, _) =>
            {
                if (NoteSecurity.Blocking || !await NoteSecurity.EnsureNoteAccessAsync(note.Id)) return;
                await RunAsync(action);
            };
            menu.Items.Add(item);
        }
        void AddProtection()
        {
            if (note.IsProtected)
            {
                Add(note.IsLocked ? "SecurityUnlock" : "SecurityLockAll", note.IsLocked ? () => ((MainWindow)App.MainWindow!).Editor.OpenNoteAsync(note.Id) : NoteSecurity.LockAsync, note.IsLocked ? "\uE785" : "\uE72E");
                Add("SecurityUnprotect", () => NoteSecurity.ProtectNoteAsync(note.Id, true), "\uE785");
            }
            else Add("SecurityProtect", () => NoteSecurity.ProtectNoteAsync(note.Id), "\uE72E");
        }

        if (!note.Trashed)
        {
            Add("LibraryOpenNote", () => ((MainWindow)App.MainWindow!).Editor.OpenNoteAsync(note.Id), "\uE8E5");
            Add(note.Favorite ? "LibraryUnstar.Label" : "LibraryStar.Label", () => ToggleFavoriteAsync(note.Id), note.Favorite ? "\uE735" : "\uE734");
            menu.Items.Add(new MenuFlyoutSeparator());
            Add("CmdRename.Label", () => RenameAsync(note.Id), "\uE70F");
            Add("ArchiveCategory", () => CategoryAsync(note.Id), "\uE8B7");
            Add("ArchiveTags", () => TagsAsync(note.Id), "\uE8EC");
            var colors = new MenuFlyoutSubItem
            {
                Text = Strings.T("ArchiveColor"),
                Icon = new FontIcon { Glyph = "\uE790" }
            };
            foreach (var color in Enum.GetValues<NoteColor>())
            {
                var item = new ToggleMenuFlyoutItem
                {
                    Text = Strings.T("NoteColor" + color),
                    IsChecked = color == note.Color
                };
                item.Click += async (_, _) => await RunAsync(() => ChangeAsync(note.Id, n => n with { Color = color }));
                colors.Items.Add(item);
            }

            menu.Items.Add(colors);
            menu.Items.Add(new MenuFlyoutSeparator());
            Add("CmdDuplicateNote.Label", () => DuplicateAsync(note.Id), "\uE8C8");
            Add("ArchiveExport", () => ExportAsync([note.Id]), "\uEDE1");
            menu.Items.Add(new MenuFlyoutSeparator());
            AddProtection();
            menu.Items.Add(new MenuFlyoutSeparator());
            Add(note.Archived ? "CmdUnarchive.Label" : "CmdArchive.Label", () => SetStateAsync(note.Id, !note.Archived, false), "\uE7B8");
            Add("ArchiveTrash", () => SetStateAsync(note.Id, note.Archived, true), "\uE74D");
            if (((MainWindow)App.MainWindow!).Editor.CurrentNote?.NoteId == note.Id)
            {
                menu.Items.Add(new MenuFlyoutSeparator());
                Add("ArchiveCloseEditor", () => ((MainWindow)App.MainWindow!).Editor.CloseEditorAsync(), "\uE711");
            }
        }
        else
        {
            Add("ArchiveRestoreNote", () => SetStateAsync(note.Id, note.Archived, false), "\uE7A7");
            Add("ArchiveDeleteForever", async () =>
            {
                var dialog = Dialog(Strings.T("ArchiveDeleteForever"), Strings.Format(Strings.DefaultMap, "LibraryDeleteConfirm", note.Title), Strings.T("ArchiveDeleteForever"));
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    return;
                await NoteArchive.Database.DeletePermanentlyAsync(note.Id);
                NoteArchive.NotifyChanged(ArchiveChangeKind.Removed, [note.Id]);
            }, "\uE74D");
            menu.Items.Add(new MenuFlyoutSeparator());
            AddProtection();
        }

        return menu;
    }

    private static async Task ChangeAsync(string id, Func<NoteSummary, NoteSummary> transform)
    {
        if (!await NoteSecurity.EnsureNoteAccessAsync(id)) return;
        var note = await NoteArchive.Database.SummaryAsync(id);
        if (note is null)
            return;
        var next = transform(note);
        await NoteArchive.Database.UpdateMetadataAsync(id, next.Title, next.Favorite, next.Color, next.CategoryId, next.Tags);
        var current = await NoteArchive.Database.SummaryAsync(id);
        if (current is not null)
            ((MainWindow)App.MainWindow!).Editor.RefreshNoteMetadata(current);
        if (current is not null) NoteArchive.NotifyChanged(ArchiveChangeKind.Metadata, current);
    }

    public static Task ToggleFavoriteAsync(string id) => ChangeAsync(id, n => n with { Favorite = !n.Favorite });
    public static async Task RenameAsync(string id)
    {
        if (!await NoteSecurity.EnsureNoteAccessAsync(id)) return;
        var note = await NoteArchive.Database.SummaryAsync(id);
        if (note is null)
            return;
        var title = await PromptAsync(Strings.T("RenameTitle"), note.Title);
        if (!string.IsNullOrWhiteSpace(title))
            await ChangeAsync(id, n => n with { Title = title.Trim() });
    }

    public static async Task CategoryAsync(string id)
    {
        if (!await NoteSecurity.EnsureNoteAccessAsync(id)) return;
        var note = await NoteArchive.Database.SummaryAsync(id);
        if (note is null)
            return;
        var labels = await NoteArchive.Database.LabelsAsync(true);
        var box = new ComboBox
        {
            MinWidth = 280,
            DisplayMemberPath = nameof(NamedLabel.Name),
            ItemsSource = new[]
            {
                new NamedLabel("", Strings.T("ArchiveUncategorized"), 0)
            }.Concat(labels).ToArray()
        };
        box.SelectedIndex = Math.Max(0, labels.ToList().FindIndex(c => c.Id == note.CategoryId) + 1);
        var name = new TextBox
        {
            PlaceholderText = Strings.T("ArchiveNewCategory")
        };
        var panel = new StackPanel
        {
            Spacing = 12
        };
        panel.Children.Add(box);
        panel.Children.Add(name);
        if (await Dialog(Strings.T("ArchiveCategory"), panel, Strings.T("Ok")).ShowAsync() != ContentDialogResult.Primary)
            return;
        if (note.IsProtected && !string.IsNullOrWhiteSpace(name.Text))
        {
            await NoteArchive.Database.SetPrivateCategoryAsync(id, name.Text);
            NoteArchive.NotifyChanged(ArchiveChangeKind.Metadata, [id]);
            return;
        }
        var categoryId = string.IsNullOrWhiteSpace(name.Text) ? (box.SelectedItem as NamedLabel)?.Id : await NoteArchive.Database.AddCategoryAsync(name.Text);
        await ChangeAsync(id, n => n with { CategoryId = string.IsNullOrEmpty(categoryId) ? null : categoryId });
    }

    public static async Task TagsAsync(string id)
    {
        if (!await NoteSecurity.EnsureNoteAccessAsync(id)) return;
        var note = await NoteArchive.Database.SummaryAsync(id);
        if (note is null)
            return;
        var tags = note.Tags.ToList();
        var known = await NoteArchive.Database.LabelsAsync(false);
        var panel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 300
        };
        var chips = new StackPanel
        {
            Spacing = 4
        };
        var input = new AutoSuggestBox
        {
            PlaceholderText = Strings.T("ArchiveAddTag")
        };
        void Refresh()
        {
            chips.Children.Clear();
            foreach (var tag in tags.ToArray())
            {
                var button = new Button
                {
                    Content = "#" + tag + "  ×"
                };
                AutomationProperties.SetName(button, Strings.T("ArchiveRemoveTag") + " " + tag);
                button.Click += (_, _) =>
                {
                    tags.Remove(tag);
                    Refresh();
                };
                chips.Children.Add(button);
            }
        }

        void Add(string value)
        {
            value = NoteDatabase.CleanLabel(value.TrimStart('#'));
            if (value.Length > 0 && !tags.Contains(value, StringComparer.OrdinalIgnoreCase))
                tags.Add(value);
            input.Text = "";
            Refresh();
        }

        input.TextChanged += (_, _) => input.ItemsSource = known.Select(t => t.Name).Where(t => t.Contains(input.Text, StringComparison.CurrentCultureIgnoreCase) && !tags.Contains(t, StringComparer.OrdinalIgnoreCase)).Take(12).ToArray();
        input.QuerySubmitted += (_, e) => Add(e.ChosenSuggestion as string ?? e.QueryText);
        panel.Children.Add(new ScrollViewer { Content = chips, MaxHeight = 220 });
        panel.Children.Add(input);
        Refresh();
        if (await Dialog(Strings.T("ArchiveTags"), panel, Strings.T("Ok")).ShowAsync() != ContentDialogResult.Primary)
            return;
        if (!string.IsNullOrWhiteSpace(input.Text))
            Add(input.Text);
        await ChangeAsync(id, n => n with { Tags = tags });
    }

    private static async Task SetStateAsync(string id, bool archived, bool trashed)
    {
        var editor = ((MainWindow)App.MainWindow!).Editor;
        if (editor.CurrentNote?.NoteId == id)
        {
            await editor.CloseEditorAsync();
            if (editor.CurrentNote is not null)
                return;
        }

        await NoteArchive.Database.SetStateAsync(id, archived, trashed);
        NoteArchive.NotifyChanged(ArchiveChangeKind.Metadata, [id]);
    }

    public static async Task DuplicateAsync(string id)
    {
        if (!await NoteSecurity.EnsureNoteAccessAsync(id)) return;
        var editor = ((MainWindow)App.MainWindow!).Editor;
        if (editor.CurrentNote?.NoteId == id && !await editor.FlushAsync())
            return;
        var note = await NoteArchive.Database.GetAsync(id);
        if (note is null)
            return;
        var copy = await NoteArchive.Database.DuplicateAsync(id, note.Summary.Title + " " + Strings.T("ArchiveCopySuffix"));
        await NoteArchive.Database.UpdateMetadataAsync(copy.Summary.Id, copy.Summary.Title, false, note.Summary.Color, note.Summary.CategoryId, note.Summary.Tags);
        NoteArchive.NotifyChanged(ArchiveChangeKind.Inserted, [copy.Summary.Id]);
        await editor.OpenNoteAsync(copy.Summary.Id);
    }

    public static async Task ImportAsync(IEnumerable<string>? paths = null)
    {
        if (paths is null)
            paths = (await FilePickerService.OpenManyMarkdownAsync()).Select(f => f.Path).ToArray();
        var items = await MarkdownTransfer.InspectAsync(paths);
        if (items.Count == 0)
            return;
        var list = new ListView
        {
            ItemsSource = items,
            DisplayMemberPath = nameof(ImportCandidate.Title),
            SelectionMode = ListViewSelectionMode.Multiple,
            MaxHeight = 360,
            MinWidth = 360
        };
        list.SelectAll();
        var attachments = new CheckBox
        {
            Content = Strings.T("ArchiveImportAttachments.Content"),
            IsChecked = true
        };
        var content = new StackPanel
        {
            Spacing = 12
        };
        content.Children.Add(list);
        content.Children.Add(attachments);
        var dialog = Dialog(Strings.T("ArchiveImport"), content, Strings.T("ArchiveImportSelected"));
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;
        await RunAsync(async () =>
        {
            var result = await NoteArchive.Transfer.ImportAsync(list.SelectedItems.Cast<ImportCandidate>().ToArray(), includeAttachments: attachments.IsChecked == true);
            NoteArchive.NotifyChanged(ArchiveChangeKind.Inserted, result.NoteIds);
            if (result.Warnings.Count > 0)
                await MessageAsync(Strings.T("ArchiveImportWarnings"), string.Join("\n", result.Warnings.Take(30)));
            if (result.NoteIds.Count == 1)
                await ((MainWindow)App.MainWindow!).Editor.OpenNoteAsync(result.NoteIds[0]);
        });
    }

    public static async Task ImportFolderAsync()
    {
        var folder = await FilePickerService.PickFolderAsync();
        if (folder is not null)
            await ImportAsync([folder.Path]);
    }

    public static async Task ExportAsync(IEnumerable<string> ids)
    {
        ids = ids.Distinct().ToArray();
        if (!await NoteSecurity.AuthorizeExportAsync(ids)) return;
        var session = NoteArchive.Database.SessionVersion;
        if (!await ((MainWindow)App.MainWindow!).Editor.FlushAsync())
            return;
        var folder = await FilePickerService.PickFolderAsync();
        if (folder is null)
            return;
        if (session != NoteArchive.Database.SessionVersion || NoteSecurity.Blocking) return;
        await RunAsync(() => NoteArchive.Transfer.ExportAsync(ids, folder.Path, NoteSecurity.OperationsToken));
    }

    public static async Task ExportCollectionAsync()
    {
        var ids = new List<string>();
        foreach (var collection in new[] { NoteCollection.All, NoteCollection.Archive })
            for (var offset = 0; ; offset += 200)
            {
                var page = await NoteArchive.Database.QueryAsync(new(Collection: collection, Offset: offset, Limit: 200));
                ids.AddRange(page.Items.Select(n => n.Id));
                if (page.Items.Count == 0 || offset + page.Items.Count >= page.Total) break;
            }
        await ExportAsync(ids);
    }

    public static async Task ExportHtmlAsync(string id, string destination)
    {
        if (!await NoteSecurity.AuthorizeExportAsync([id])) return;
        var token = NoteSecurity.OperationsToken;
        if (!await ((MainWindow)App.MainWindow!).Editor.FlushAsync())
            return;
        var note = await NoteArchive.Database.GetAsync(id);
        if (note is null)
            return;
        var markdown = note.Markdown;
        foreach (var match in MarkdownReference.Parse(markdown).Where(r => r.Target.StartsWith("attachment:", StringComparison.OrdinalIgnoreCase)).OrderByDescending(r => r.Start))
        {
            var asset = match.Target[11..].TrimStart('/').Split('#')[0];
            var name = await NoteArchive.Database.AttachmentNameAsync(asset, id);
            if (name is null)
                continue;
            var folder = Path.Combine(Path.GetDirectoryName(destination)!, Path.GetFileNameWithoutExtension(destination) + "_assets");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, asset + Path.GetExtension(name));
            using (var output = File.Create(path))
                await NoteArchive.Database.CopyAttachmentAsync(asset, output, token, id);
            markdown = markdown.Remove(match.Start, match.Length).Insert(match.Start, match.ReplaceWith(Path.GetRelativePath(Path.GetDirectoryName(destination)!, path).Replace('\\', '/')));
        }

        var options = SettingsService.Instance.Export;
        var html = await Task.Run(() => ExportHtmlService.Export(markdown, note.Summary.Title, options, destination));
        token.ThrowIfCancellationRequested();
        await File.WriteAllTextAsync(destination, html, token);
    }

    public static async Task<NoteSummary?> PickNoteAsync(IReadOnlyList<NoteSummary>? choices = null)
    {
        var input = new TextBox
        {
            PlaceholderText = Strings.T("ArchiveSearchNotes")
        };
        var list = new ListView
        {
            DisplayMemberPath = nameof(MarkdownMkII.ViewModels.ArchiveNoteCard.Title),
            IsItemClickEnabled = true,
            MaxHeight = 340
        };
        var panel = new StackPanel
        {
            Spacing = 8,
            MinWidth = 340
        };
        panel.Children.Add(input);
        panel.Children.Add(list);
        var dialog = Dialog(Strings.T("ArchiveChooseNote"), panel);
        NoteSummary? chosen = null;
        var version = 0;
        async Task Refresh()
        {
            var current = ++version;
            var session = NoteArchive.Database.SessionVersion;
            var notes = choices?.Where(n => n.Title.Contains(input.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray() ?? (await NoteArchive.Database.QueryAsync(new(Search: input.Text, Limit: 100))).Items;
            if (current == version && session == NoteArchive.Database.SessionVersion && !NoteSecurity.Blocking)
            {
                list.ItemsSource = notes.Select(n => new MarkdownMkII.ViewModels.ArchiveNoteCard(n)).ToArray();
                if (list.Items.Count > 0)
                    list.SelectedIndex = 0;
            }
        }

        input.TextChanged += async (_, _) => await Refresh();
        list.ItemClick += (_, e) =>
        {
            chosen = (e.ClickedItem as MarkdownMkII.ViewModels.ArchiveNoteCard)?.Note;
            dialog.Hide();
        };
        input.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter && list.SelectedItem is MarkdownMkII.ViewModels.ArchiveNoteCard note)
            {
                chosen = note.Note;
                args.Handled = true;
                dialog.Hide();
            }
            else if (args.Key is Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down && list.Items.Count > 0)
            {
                list.SelectedIndex = Math.Clamp(list.SelectedIndex + (args.Key == Windows.System.VirtualKey.Down ? 1 : -1), 0, list.Items.Count - 1);
                list.ScrollIntoView(list.SelectedItem);
                args.Handled = true;
            }
        };
        dialog.Opened += (_, _) => input.Focus(FocusState.Programmatic);
        await Refresh();
        await dialog.ShowAsync();
        version++;
        list.ItemsSource = null;
        return chosen;
    }
}

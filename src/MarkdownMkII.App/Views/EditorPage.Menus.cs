using MarkdownMkII.Services;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Editor;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace MarkdownMkII.Views;

public sealed partial class EditorPage
{
    private IReadOnlyDictionary<string, PaletteCommand> menuCommands = null!;

    private void InitializeEditorMenus()
    {
        // Menus reuse the palette's labels, commands and shortcuts; one source of truth.
        menuCommands = EditorPalette.Commands(ViewModel).ToDictionary(command => command.Id);
        foreach (var (button, key) in new[]
        {
            (DocumentMenuButton, "EditorMenuDocument"), (FormatMenuButton, "EditorMenuFormat"),
            (InsertMenuButton, "EditorMenuInsert"), (ViewMenuButton, "EditorMenuView")
        })
        {
            var label=Strings.T(key);
            var content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6};
            content.Children.Add(new TextBlock{Text=label});
            content.Children.Add(new FontIcon{Glyph="\uE70D",FontSize=9,VerticalAlignment=VerticalAlignment.Center});
            button.Content=content;
            AutomationProperties.SetName(button,label);
        }
        RefreshCommandTooltips();
        EditorBox.ContextFlyout = CreateMenu(ContextItems);
        DocumentMenuButton.Flyout = CreateMenu(DocumentItems);
        FormatMenuButton.Flyout = CreateMenu(FormatItems);
        InsertMenuButton.Flyout = CreateMenu(InsertItems);
        ViewMenuButton.Flyout = CreateMenu(ViewItems);
    }

    private async void OnToolbarCommand(object sender, RoutedEventArgs e)
    {
        if(sender is FrameworkElement{Tag:string id})await ShortcutService.ExecuteAsync(menuCommands[id]);
    }
    private void OnToolbarAvailableSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if(BoldButton is null)return;
        var width=e.NewSize.Width-(MinimapColumn?.Width.Value??0);
        BoldButton.Visibility=ItalicButton.Visibility=width<530?Visibility.Collapsed:Visibility.Visible;
        FloatingToolbar.MaxWidth=Math.Max(260,width-16);
    }
    private void OnToolbarContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if(EditorRoot is not null)EditorRoot.RowDefinitions[0].Height=new GridLength(Math.Max(52,e.NewSize.Height+14));
    }

    private void RefreshCommandTooltips()
    {
        foreach (var (button,id) in new[] { (BoldButton,"bold"),(ItalicButton,"italic"),(AllCommandsButton,"palette"),(CloseEditorButton,"close") })
        {
            var command = menuCommands[id];
            ToolTipService.SetToolTip(button,command.Label + (command.Shortcut is null ? "" : " · " + command.Shortcut));
            AutomationProperties.SetName(button,command.Label);
        }
    }

    private MenuFlyout CreateMenu(Func<IEnumerable<MenuFlyoutItemBase>> build)
    {
        var flyout = new MenuFlyout();
        flyout.Opening += (_, _) =>
        {
            commandBarOpen = true;
            chromeHideTimer.Stop();
            flyout.Items.Clear();
            foreach (var item in build()) flyout.Items.Add(item);
        };
        flyout.Closed += (_, _) =>
        {
            commandBarOpen = false;
            if (immersive) chromeHideTimer.Start();
        };
        return flyout;
    }

    private MenuFlyoutItem CommandItem(string id, Symbol? icon = null)
    {
        var command = menuCommands[id];
        var item = new MenuFlyoutItem
        {
            Text = command.Label,

            KeyboardAcceleratorTextOverride = command.Shortcut ?? string.Empty,
            Icon = icon is { } symbol ? new SymbolIcon(symbol) : null
        };
        item.IsEnabled = command.Command.CanExecute(null);
        item.Click += async (_, _) => await ShortcutService.ExecuteAsync(command);
        return item;
    }

    private static MenuFlyoutItem ActionItem(string key, Action action, bool enabled = true, string shortcut = "", Symbol? icon = null)
    {
        var item = new MenuFlyoutItem
        {
            Text = Strings.T(key), IsEnabled = enabled,
            KeyboardAcceleratorTextOverride = shortcut,
            Icon = icon is { } symbol ? new SymbolIcon(symbol) : null
        };
        item.Click += (_, _) => action();
        return item;
    }

    private static MenuFlyoutSubItem Group(string key, IEnumerable<MenuFlyoutItemBase> items)
    {
        var group = new MenuFlyoutSubItem { Text = Strings.T(key) };
        foreach (var item in items) group.Items.Add(item);
        return group;
    }

    private MenuFlyoutSubItem CommandsGroup(string key, params string[] ids)
        => Group(key, ids.Select(id => CommandItem(id)));

    private IEnumerable<MenuFlyoutItemBase> ContextItems()
    {
        var tab = ViewModel.CurrentNote;
        if (tab is null) yield break;
        var (start, length) = GetSelection();
        var context = EditorContext.Inspect(tab.Text, start, length,
            tab.Parsed?.Text == tab.Text ? tab.Parsed.Syntax : null);

        yield return ActionItem("EditorMenuCut", () => CopySelection(cut: true), context.HasSelection, "Ctrl+X", Symbol.Cut);
        yield return ActionItem("EditorMenuCopy", () => CopySelection(cut: false), context.HasSelection, "Ctrl+C", Symbol.Copy);
        yield return ActionItem("EditorMenuPaste", async () =>
        {
            if (ReferenceEquals(tab, ViewModel.CurrentNote)) await PasteIntoEditorAsync(tab, Clipboard.GetContent());
        }, CanPasteIntoEditor(), "Ctrl+V", Symbol.Paste);
        yield return ActionItem("EditorMenuSelectAll", () => { SetSelection(0, tab.Text.Length); FocusEditor(); }, tab.Text.Length > 0, "Ctrl+A");
        yield return new MenuFlyoutSeparator();
        yield return CommandItem("undo", Symbol.Undo);
        yield return CommandItem("redo", Symbol.Redo);
        yield return new MenuFlyoutSeparator();
        yield return Group("EditorMenuFormat", FormatItems());
        yield return Group("EditorMenuInsert", InsertItems());
        yield return CommandsGroup("EditorMenuLines", "selectLine", "duplicate", "deleteLine", "moveUp", "moveDown", "join", "sort", "reverseLines", "reflow");
        if (context.IsTable) yield return Group("EditorMenuTable", TableItems());
        if (context.IsList) yield return CommandsGroup("EditorMenuList", "indentList", "outdentList", "duplicateListItem", "deleteListItem", "moveListUp", "moveListDown", "checkTasks", "uncheckTasks");
        if (context.IsHeading) yield return HeadingActions();
        if (context.HasSelection) yield return CommandItem("extractNote");
        yield return new MenuFlyoutSeparator();
        yield return CommandItem("palette", Symbol.Find);
    }

    private IEnumerable<MenuFlyoutItemBase> FormatItems() =>
    [
        CommandItem("bold", Symbol.Bold), CommandItem("italic", Symbol.Italic),
        CommandItem("code"), CommandItem("strike"), CommandItem("highlight"),
        CommandsGroup("EditorMenuMoreStyles", "underline", "superscript", "subscript"),
        new MenuFlyoutSeparator(),
        Group("EditorMenuHeadings", HeadingLevels()),
        CommandsGroup("EditorMenuLists", "bullet", "numbered", "task", "definition"),
        CommandItem("quote"), CommandItem("comment"),
        CommandsGroup("EditorMenuTransform", "upper", "lower", "titleCase", "sentenceCase", "typographer", "format", "stripTrailing")
    ];

    private IEnumerable<MenuFlyoutItemBase> HeadingLevels()
    {
        yield return CommandItem("h1");
        yield return CommandItem("h2");
        yield return CommandItem("h3");
        yield return CommandItem("h4");
        yield return CommandItem("h5");
        yield return CommandItem("h6");
        yield return new MenuFlyoutSeparator();
        yield return CommandItem("cycleHeading");
        yield return CommandItem("promoteHeading");
        yield return CommandItem("demoteHeading");
    }

    private IEnumerable<MenuFlyoutItemBase> InsertItems() =>
    [
        CommandItem("link", Symbol.Link), CommandItem("image", Symbol.Pictures),
        CommandItem("wiki"), CommandItem("footnote"),
        new MenuFlyoutSeparator(),
        CommandItem("table"), CommandItem("fence"), CommandItem("callout"), CommandItem("rule"),
        CommandsGroup("EditorMenuMathDiagrams", "math", "mathBlock", "mermaid"),
        Group("EditorMenuStructure", [CommandItem("toc"), CommandItem("timestamp"),
            ActionItem("CmdFrontMatter.Label", () => ViewModel.FrontMatterCommand.Execute(null), ViewModel.CurrentNote is not null)])
    ];

    private IEnumerable<MenuFlyoutItemBase> TableItems() =>
    [
        CommandItem("addTableRow"), CommandItem("duplicateTableRow"), CommandItem("deleteTableRow"),
        CommandsGroup("EditorMenuMoveRows", "moveTableRowUp", "moveTableRowDown"),
        CommandsGroup("EditorMenuColumns", "addTableColumn", "duplicateTableColumn", "deleteTableColumn", "moveTableLeft", "moveTableRight"),
        new MenuFlyoutSeparator(),
        CommandsGroup("EditorMenuAlignment", "alignLeft", "alignCenter", "alignRight"),
        CommandItem("formatTable"), CommandItem("transposeTable"),
        CommandsGroup("EditorMenuSort", "sortTable", "sortTableDesc"),
        CommandsGroup("EditorMenuCopyAs", "copyTable", "copyTableTsv", "copyTableHtml")
    ];

    private MenuFlyoutSubItem HeadingActions() => CommandsGroup("EditorMenuHeadingSection",
        "renameHeading", "selectHeading", "moveHeadingUp", "moveHeadingDown", "promoteHeading", "demoteHeading", "foldHeading", "copyHeading");

    private MenuFlyoutItem FavoriteItem()
    {
        var item=CommandItem("star",ViewModel.CurrentNote?.Metadata?.Favorite==true?Symbol.SolidStar:Symbol.OutlineStar);
        item.Text=Strings.T(ViewModel.CurrentNote?.Metadata?.Favorite==true?"LibraryUnstar.Label":"LibraryStar.Label");
        return item;
    }

    private IEnumerable<MenuFlyoutItemBase> DocumentItems() =>
    [
        CommandItem("new", Symbol.Add), CommandItem("open", Symbol.OpenFile), CommandItem("quickOpen", Symbol.Find),
        new MenuFlyoutSeparator(),
        CommandItem("rename"), CommandItem("duplicateNote"), FavoriteItem(),
        ActionItem("ArchiveCategory", async () => { if (ViewModel.CurrentNote?.NoteId is { } id) await ArchiveDialogs.CategoryAsync(id); }),
        ActionItem("ArchiveTags", async () => { if (ViewModel.CurrentNote?.NoteId is { } id) await ArchiveDialogs.TagsAsync(id); }),
        new MenuFlyoutSeparator(),
        CommandsGroup("EditorMenuExportShare", "exportMd", "export", "print", "share", "copyMd", "copyHtml"),
        CommandItem(ViewModel.CurrentNote?.Metadata?.IsProtected == true ? "unprotectNote" : "protectNote"),
        CommandItem("lockNotes"), CommandItem("history"), CommandItem("close")
    ];

    private IEnumerable<MenuFlyoutItemBase> ViewItems()
    {
        var mode = ViewModel.CurrentNote?.ViewMode;
        foreach (var (id, value) in new[]
        {
            ("viewEditor", EditorViewMode.Editor), ("viewPreview", EditorViewMode.Preview),
            ("viewSplit", EditorViewMode.Split), ("viewSplitVertical", EditorViewMode.SplitVertical)
        })
        {
            var command = menuCommands[id];
            var item = new ToggleMenuFlyoutItem { Text = command.Label, IsChecked = mode == value, IsEnabled = mode is not null, KeyboardAcceleratorTextOverride = command.Shortcut ?? "" };
            item.Click += async (_, _) => await ShortcutService.ExecuteAsync(command);
            yield return item;
        }
        yield return new MenuFlyoutSeparator();
        yield return CommandItem("inspector");
        yield return CommandItem("wrap");
        yield return CommandsGroup("EditorMenuNavigation", "find", "replace", "goToLine", "goToHeading", "goToDefinition", "nextIssue", "prevIssue");
        yield return CommandsGroup("EditorMenuReading", "focus", "immersive", "slides", "kanban", "diff", "zoomIn", "zoomOut");
        yield return CommandsGroup("EditorMenuSections", "foldHeading", "foldAll", "unfoldAll");
        yield return CommandItem("refreshPreview");
        yield return ActionItem("WorkspaceLibrary.Label", () => OnOpenLibrary(this, new RoutedEventArgs()), icon: Symbol.Library);
    }

    private static bool CanPasteIntoEditor()
    {
        try
        {
            var view = Clipboard.GetContent();
            return view.Contains(StandardDataFormats.Text) || view.Contains(StandardDataFormats.Bitmap) || view.Contains(StandardDataFormats.StorageItems);
        }
        catch (Exception) { return false; }
    }

    private async void CopySelection(bool cut)
    {
        if (ViewModel.CurrentNote is not { } tab) return;
        var (start, length) = GetSelection();
        start = Math.Clamp(start, 0, tab.Text.Length);
        length = Math.Clamp(length, 0, tab.Text.Length - start);
        if (length == 0) return;
        try
        {
            if (!await NoteSecurity.ConfirmCopyAsync(tab) || !ReferenceEquals(tab, ViewModel.CurrentNote)) return;
            var package = new DataPackage();
            package.SetText(tab.Text.Substring(start, length));
            Clipboard.SetContent(package);
            if (cut) ViewModel.ApplyEdit((text, offset, count) => MarkdownEditing.InsertText(text, offset, count, string.Empty));
        }
        catch (Exception ex) { Services.DiagnosticsService.LogError("Editor", "Copia della selezione non riuscita", ex); }
    }
}

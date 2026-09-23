using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Windows.ApplicationModel.DataTransfer;

namespace MarkdownMkII.ViewModels;

public partial class EditorViewModel : ObservableObject
{
    private IEditorHost? host;
    private IRelayCommand[]? selectionCommands;
    [ObservableProperty] public partial NoteEditorViewModel? CurrentNote { get; set; }
    public bool HasUnsavedChanges => CurrentNote?.IsDirty == true;

    public void AttachHost(IEditorHost editorHost)
    {
        ArgumentNullException.ThrowIfNull(editorHost);
        host = editorHost;
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void Print() => host?.Print();

    [RelayCommand]
    public void ToggleFind()
    {
        if (CurrentNote is null)
        {
            return;
        }

        CurrentNote.FindBarOpen = !CurrentNote.FindBarOpen;
        host?.ShowFind();
    }

    public bool TryContinueBlock()
    {
        if (CurrentNote is null || host is null)
        {
            return false;
        }

        var (start, length) = host.GetSelection();
        if (length != 0)
        {
            return false;
        }

        var result = MarkdownEditing.ContinueBlockOnEnter(CurrentNote.Text, start);
        if (result is not { } edit)
        {
            return false;
        }

        ApplyResult(CurrentNote, edit);
        return true;
    }

    public void ApplyEdit(Func<string, int, int, EditResult> transform)
    {
        if (CurrentNote is { } tab && host is not null) ApplyEdit(tab, transform);
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo() => ApplyHistory(undo: true);

    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo() => ApplyHistory(undo: false);

    private bool CanUndo() => CurrentNote?.CanUndo == true;
    private bool CanRedo() => CurrentNote?.CanRedo == true;

    private void ApplyHistory(bool undo)
    {
        if (CurrentNote is not { } tab) return;
        var edit = undo ? tab.Undo() : tab.Redo();
        if (edit is { } restored)
            host?.ApplyText(restored.Text, restored.SelectionStart, restored.SelectionLength);
    }

    private void ApplyEdit(NoteEditorViewModel tab, Func<string, int, int, EditResult> transform, bool focus = true)
    {
        if (!ReferenceEquals(tab, CurrentNote)) return;
        var selection = ReferenceEquals(tab, CurrentNote)
            ? host?.GetSelection() ?? (tab.CaretStart, 0)
            : (tab.CaretStart, 0);
        var start = Math.Clamp(selection.Item1, 0, tab.Text.Length);
        var length = Math.Clamp(selection.Item2, 0, tab.Text.Length - start);
        tab.UpdateSelection(start, length);
        var result = transform(tab.Text, start, length);
        // Whole-document transforms report (0, 0) when they do not compute a selection.
        if (result.SelectionStart == 0 && result.SelectionLength == 0 && (start != 0 || length != 0))
            result = MarkdownEditing.MapSelection(tab.Text, result.Text, start, length);
        ApplyResult(tab, result, focus);
    }

    private void ApplyResult(NoteEditorViewModel tab, EditResult result, bool focus = true)
    {
        var changed = !string.Equals(tab.Text, result.Text, StringComparison.Ordinal);
        var edit = tab.ApplyEdit(result);
        if (!ReferenceEquals(tab, CurrentNote) || host is null) return;

        if (changed)
            host.ApplyText(edit.Text, edit.SelectionStart, edit.SelectionLength);
        else
            host.SetSelection(edit.SelectionStart, edit.SelectionLength);
        if (focus) host.FocusEditor();
    }

    [RelayCommand] public void Bold() => ApplyEdit(MarkdownEditing.ToggleBold);
    [RelayCommand] public void Italic() => ApplyEdit(MarkdownEditing.ToggleItalic);
    [RelayCommand]
    public async Task GoToLineAsync()
    {
        if (CurrentNote is not { } tab || host is null)
        {
            return;
        }

        var value = await host.PromptAsync(Strings.T("GoToLine"), "1");
        if (!int.TryParse(value, out var line) || line < 1 || !ReferenceEquals(tab, CurrentNote))
        {
            return;
        }

        var map = new LineMap(CurrentNote.Text);
        var offset = map.OffsetOfLine(line - 1);
        host.SetSelection(offset, 0);
        host.FocusEditor();
    }

    [RelayCommand]
    public async Task LinkAsync()
    {
        if (CurrentNote is not { } sourceTab) return;
        if (host is null)
        {
            return;
        }

        var url = await host.PromptAsync(Strings.T("LinkUrl"), "https://");
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        ApplyEdit(sourceTab, (t, s, l) => MarkdownEditing.InsertLink(t, s, l, url.Trim()));
    }

    [RelayCommand] public void Strike() => ApplyEdit(MarkdownEditing.ToggleStrikethrough);
    [RelayCommand] public void InlineCode() => ApplyEdit(MarkdownEditing.ToggleInlineCode);
    [RelayCommand] public void Highlight() => ApplyEdit(MarkdownEditing.ToggleHighlight);
    [RelayCommand] public void Superscript() => ApplyEdit(MarkdownEditing.ToggleSuperscript);
    [RelayCommand] public void Subscript() => ApplyEdit(MarkdownEditing.ToggleSubscript);
    [RelayCommand] public void Inserted() => ApplyEdit(MarkdownEditing.ToggleInserted);
    [RelayCommand] public void Heading1() => ApplyEdit((t, s, l) => MarkdownEditing.ToggleHeading(t, s, l, 1));
    [RelayCommand] public void Heading2() => ApplyEdit((t, s, l) => MarkdownEditing.ToggleHeading(t, s, l, 2));
    [RelayCommand] public void Heading3() => ApplyEdit((t, s, l) => MarkdownEditing.ToggleHeading(t, s, l, 3));
    [RelayCommand] public void Heading4() => ApplyEdit((t, s, l) => MarkdownEditing.ToggleHeading(t, s, l, 4));
    [RelayCommand] public void Heading5() => ApplyEdit((t, s, l) => MarkdownEditing.ToggleHeading(t, s, l, 5));
    [RelayCommand] public void Heading6() => ApplyEdit((t, s, l) => MarkdownEditing.ToggleHeading(t, s, l, 6));
    [RelayCommand] public void CycleHeading() => ApplyEdit(MarkdownEditing.CycleHeading);
    [RelayCommand] public void PromoteHeading() => ApplyEdit((t, s, l) => MarkdownEditing.ShiftHeadingLevel(t, s, l, -1));
    [RelayCommand] public void DemoteHeading() => ApplyEdit((t, s, l) => MarkdownEditing.ShiftHeadingLevel(t, s, l, 1));
    [RelayCommand] public void BulletList() => ApplyEdit((t, s, l) => MarkdownEditing.ToggleList(t, s, l, false));
    [RelayCommand] public void NumberedList() => ApplyEdit((t, s, l) => MarkdownEditing.ToggleList(t, s, l, true));
    [RelayCommand] public void TaskList() => ApplyEdit(MarkdownEditing.ToggleTaskItem);
    [RelayCommand] public void CheckTasks() => ApplyEdit((t, s, l) => MarkdownEditing.SetTasksChecked(t, s, l, true));
    [RelayCommand] public void UncheckTasks() => ApplyEdit((t, s, l) => MarkdownEditing.SetTasksChecked(t, s, l, false));
    [RelayCommand] public void Quote() => ApplyEdit(MarkdownEditing.ToggleQuote);
    [RelayCommand] public void NestQuote() => ApplyEdit(MarkdownEditing.NestQuote);
    [RelayCommand] public void UnnestQuote() => ApplyEdit(MarkdownEditing.UnnestQuote);
    [RelayCommand] public void CodeFence() => ApplyEdit((t, s, l) => MarkdownEditing.InsertCodeFence(t, s, l, "csharp"));
    [RelayCommand] public void HorizontalRule() => ApplyEdit((t, s, l) => MarkdownEditing.InsertHorizontalRule(t, s));
    [RelayCommand] public void DefinitionList() => ApplyEdit(MarkdownEditing.InsertDefinitionList);
    [RelayCommand] public void Table() => ApplyEdit((t, s, l) => MarkdownEditing.InsertTable(t, s, 2, 3));
    [RelayCommand] public void InsertTabSpaces() => ApplyEdit((t, s, l) => MarkdownEditing.InsertText(t, s, l, new string(' ', SettingsService.Instance.Editor.TabSize)));
    [RelayCommand] public void Indent() => ApplyEdit((t, s, l) => MarkdownEditing.Indent(t, s, l, SettingsService.Instance.Editor.TabSize));
    [RelayCommand] public void Unindent() => ApplyEdit((t, s, l) => MarkdownEditing.Unindent(t, s, l, SettingsService.Instance.Editor.TabSize));
    [RelayCommand] public void AddTableRow() => ApplyEdit((t, s, l) => MarkdownEditing.AddTableRow(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void DuplicateTableRow() => ApplyEdit((t, s, l) => MarkdownEditing.DuplicateTableRow(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void DeleteTableRow() => ApplyEdit((t, s, l) => MarkdownEditing.DeleteTableRow(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void AddTableColumn() => ApplyEdit((t, s, l) => MarkdownEditing.AddTableColumn(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void DeleteTableColumn() => ApplyEdit((t, s, l) => MarkdownEditing.DeleteTableColumn(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void MoveTableColumnLeft() => ApplyEdit((t, s, l) => MarkdownEditing.MoveTableColumn(t, s, l, -1) ?? new EditResult(t, s, l));
    [RelayCommand] public void MoveTableColumnRight() => ApplyEdit((t, s, l) => MarkdownEditing.MoveTableColumn(t, s, l, 1) ?? new EditResult(t, s, l));
    [RelayCommand] public void DuplicateTableColumn() => ApplyEdit((t, s, l) => MarkdownEditing.DuplicateTableColumn(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void TableToList() => ApplyEdit((t, s, l) => MarkdownEditing.TableToList(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void ListToTable() => ApplyEdit((t, s, l) => MarkdownEditing.ListToTable(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void MoveTableRowUp() => ApplyEdit((t, s, l) => MarkdownEditing.MoveTableRow(t, s, l, -1) ?? new EditResult(t, s, l));
    [RelayCommand] public void MoveTableRowDown() => ApplyEdit((t, s, l) => MarkdownEditing.MoveTableRow(t, s, l, 1) ?? new EditResult(t, s, l));
    [RelayCommand]
    public void CopyTable()
    {
        if (CurrentNote is null || host is null)
        {
            return;
        }

        var (start, _) = host.GetSelection();
        var csv = MarkdownEditing.TableToDelimited(CurrentNote.Text, start);
        if (csv is null)
        {
            return;
        }

        _ = CopyToClipboardAsync(csv);
    }

    [RelayCommand]
    public void CopyTableTsv()
    {
        if (CurrentNote is null || host is null)
        {
            return;
        }

        var (start, _) = host.GetSelection();
        var tsv = MarkdownEditing.TableToDelimited(CurrentNote.Text, start, '\t');
        if (tsv is null)
        {
            return;
        }

        _ = CopyToClipboardAsync(tsv);
    }

    [RelayCommand]
    public void CopyTableHtml()
    {
        if (CurrentNote is null || host is null)
        {
            return;
        }

        var (start, _) = host.GetSelection();
        var html = MarkdownEditing.TableToHtml(CurrentNote.Text, start);
        if (html is null)
        {
            return;
        }

        _ = CopyToClipboardAsync(html);
    }

    [RelayCommand]
    public void FormatTable() => ApplyEdit((t, s, l) => MarkdownEditing.FormatTable(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand]
    public void TableFromCsv()
        => ApplyEdit((t, s, l) => MarkdownEditing.InsertTableFromDelimited(
            t,
            s,
            l,
            l == 0 ? string.Empty : t.Substring(s, l)));
    [RelayCommand] public void AlignTableLeft() => ApplyEdit((t, s, l) => MarkdownEditing.AlignTableColumn(t, s, l, "left") ?? new EditResult(t, s, l));
    [RelayCommand] public void AlignTableCenter() => ApplyEdit((t, s, l) => MarkdownEditing.AlignTableColumn(t, s, l, "center") ?? new EditResult(t, s, l));
    [RelayCommand] public void AlignTableRight() => ApplyEdit((t, s, l) => MarkdownEditing.AlignTableColumn(t, s, l, "right") ?? new EditResult(t, s, l));
    [RelayCommand] public void SortTable() => ApplyEdit((t, s, l) => MarkdownEditing.SortTableColumn(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void SortTableDescending() => ApplyEdit((t, s, l) => MarkdownEditing.SortTableColumn(t, s, l, descending: true) ?? new EditResult(t, s, l));
    [RelayCommand] public void TransposeTable() => ApplyEdit((t, s, l) => MarkdownEditing.TransposeTable(t, s, l) ?? new EditResult(t, s, l));
    [RelayCommand] public void StripFrontMatter() => ApplyEdit((t, s, l) => MarkdownEditing.StripFrontMatter(t));
    [RelayCommand] public void MoveLineUp() => ApplyEdit((t, s, l) => MarkdownEditing.MoveLines(t, s, l, -1));
    [RelayCommand] public void MoveLineDown() => ApplyEdit((t, s, l) => MarkdownEditing.MoveLines(t, s, l, 1));
    [RelayCommand] public void MoveHeadingUp() => ApplyEdit((t, s, l) => MarkdownEditing.MoveHeadingSection(t, s, -1) ?? MarkdownEditing.MoveLines(t, s, l, -1));
    [RelayCommand] public void MoveHeadingDown() => ApplyEdit((t, s, l) => MarkdownEditing.MoveHeadingSection(t, s, 1) ?? MarkdownEditing.MoveLines(t, s, l, 1));
    [RelayCommand] public void SelectHeading() => ApplyEdit((t, s, l) => MarkdownEditing.SelectHeadingSection(t, s) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ToggleFoldHeading()
    {
        if (CurrentNote is null || host is null)
        {
            return;
        }

        var line = CurrentNote.Parsed?.Lines.LineOfOffset(host.GetSelection().Start) ?? 0;
        if (!HeadingFold.Toggle(CurrentNote.FoldedHeadings, CurrentNote.Outline, line) &&
            !FenceFold.Toggle(CurrentNote.FoldedHeadings, CurrentNote.Text, line) &&
            !ListFold.Toggle(CurrentNote.FoldedHeadings, CurrentNote.Text, line))
        {
            return;
        }

        CurrentNote.NotifyOutline();
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void FoldAllHeadings()
    {
        if (CurrentNote is null)
        {
            return;
        }

        HeadingFold.FoldAll(CurrentNote.FoldedHeadings, CurrentNote.Outline, CurrentNote.Text);
        CurrentNote.NotifyOutline();
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void UnfoldAllHeadings()
    {
        if (CurrentNote is null)
        {
            return;
        }

        HeadingFold.UnfoldAll(CurrentNote.FoldedHeadings);
        CurrentNote.NotifyOutline();
    }

    public void ToggleFoldHeadingAt(int line)
    {
        if (CurrentNote is null)
        {
            return;
        }

        if (!HeadingFold.Toggle(CurrentNote.FoldedHeadings, CurrentNote.Outline, line) &&
            !FenceFold.Toggle(CurrentNote.FoldedHeadings, CurrentNote.Text, line) &&
            !ListFold.Toggle(CurrentNote.FoldedHeadings, CurrentNote.Text, line))
        {
            return;
        }

        CurrentNote.NotifyOutline();
    }

    [RelayCommand] public void DuplicateLine() => ApplyEdit(MarkdownEditing.DuplicateLines);
    [RelayCommand] public void DeleteLine() => ApplyEdit(MarkdownEditing.DeleteLines);
    [RelayCommand] public void InsertMath() => ApplyEdit(MarkdownEditing.InsertMath);
    [RelayCommand] public void InsertMathBlock() => ApplyEdit(MarkdownEditing.InsertMathBlock);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void InsertToc()
        => ApplyEdit((t, s, l) => MarkdownEditing.InsertTableOfContents(t, s, Strings.T("TocHeading")));

    public void GoToSourceLine(int line)
    {
        if (CurrentNote is null || host is null)
        {
            return;
        }

        var map = new LineMap(CurrentNote.Text);
        var offset = map.OffsetOfLine(Math.Clamp(line, 0, Math.Max(0, map.LineCount - 1)));
        host.SetSelection(offset, 0);
        host.FocusEditor();
    }

    public void GoToHeading(string id)
    {
        if (CurrentNote is null)
        {
            return;
        }

        var node = CurrentNote.Outline.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Title, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Title.Replace(' ', '-'), id, StringComparison.OrdinalIgnoreCase));
        if (node is not null)
        {
            GoToSourceLine(node.SourceLine);
            return;
        }

        var marker = id.StartsWith('^') ? id : "^" + id;
        var map = new LineMap(CurrentNote.Text);
        for (var line = 0; line < map.LineCount; line++)
        {
            var trimmed = map.LineText(line).TrimEnd();
            if (trimmed.Equals(marker, StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith(" " + marker, StringComparison.OrdinalIgnoreCase))
            {
                GoToSourceLine(line);
                return;
            }
        }
    }

    public void GoToFootnote(string label)
    {
        if (CurrentNote is null || host is null || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var needle = "[^" + label + "]:";
        var index = CurrentNote.Text.IndexOf(needle, StringComparison.Ordinal);
        if (index < 0)
        {
            return;
        }

        host.SetSelection(index, 0);
        host.FocusEditor();
    }

    [RelayCommand]
    public void SelectLine()
    {
        if (CurrentNote is null || host is null)
        {
            return;
        }

        var (start, length) = host.GetSelection();
        var range = MarkdownEditing.LineSelection(CurrentNote.Text, start, length);
        host.SetSelection(range.Start, range.Length);
        host.FocusEditor();
    }

    public bool TryAdvanceTableCell(bool reverse)
    {
        if (CurrentNote is null || host is null)
        {
            return false;
        }

        var (start, length) = host.GetSelection();
        var result = MarkdownEditing.AdvanceTableCell(CurrentNote.Text, start, length, reverse);
        if (result is not { } edit)
        {
            return false;
        }

        ApplyResult(CurrentNote, edit);

        return true;
    }

    public bool TryToggleTask()
    {
        if (CurrentNote is null || host is null)
        {
            return false;
        }

        var (start, _) = host.GetSelection();
        var result = MarkdownEditing.ToggleTaskAtCaret(CurrentNote.Text, start);
        if (result is not { } edit)
        {
            return false;
        }

        ApplyResult(CurrentNote, edit);
        return true;
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void GoToNextIssue() => GoToIssue(1);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void GoToPreviousIssue() => GoToIssue(-1);

    private void GoToIssue(int direction)
    {
        if (CurrentNote?.Parsed is null || host is null)
        {
            return;
        }

        var issues = CurrentNote.Parsed.Diagnostics;
        if (issues.Count == 0)
        {
            return;
        }

        var (start, _) = host.GetSelection();
        var line = new LineMap(CurrentNote.Text).LineOfOffset(start);
        MarkdownIssue? target = null;
        if (direction > 0)
        {
            target = issues.FirstOrDefault(issue => issue.Line > line) ?? issues[0];
        }
        else
        {
            for (var i = issues.Count - 1; i >= 0; i--)
            {
                if (issues[i].Line < line)
                {
                    target = issues[i];
                    break;
                }
            }

            target ??= issues[^1];
        }

        GoToSourceLine(target.Line);
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void FormatDocument() => ApplyEdit((t, s, l) => MarkdownEditing.FormatDocument(t));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ToggleComment() => ApplyEdit(MarkdownEditing.ToggleHtmlComment);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void SortLines() => ApplyEdit(MarkdownEditing.SortSelectedLines);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ReverseLines() => ApplyEdit(MarkdownEditing.ReverseSelectedLines);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void SortList() => ApplyEdit((t, s, l) => MarkdownEditing.SortListItems(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void DuplicateListItem()
        => ApplyEdit((t, s, l) => MarkdownEditing.DuplicateListItem(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void MoveListItemUp()
        => ApplyEdit((t, s, l) => MarkdownEditing.MoveListItem(t, s, l, -1) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void MoveListItemDown()
        => ApplyEdit((t, s, l) => MarkdownEditing.MoveListItem(t, s, l, 1) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void DeleteListItem()
        => ApplyEdit((t, s, l) => MarkdownEditing.DeleteListItem(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ConvertToOrderedList()
        => ApplyEdit((t, s, l) => MarkdownEditing.ConvertList(t, s, l, ordered: true) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ConvertToBulletList()
        => ApplyEdit((t, s, l) => MarkdownEditing.ConvertList(t, s, l, ordered: false) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void CycleBulletMarker()
        => ApplyEdit((t, s, l) => MarkdownEditing.CycleBulletMarker(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void IndentList()
        => ApplyEdit((t, s, l) => MarkdownEditing.IndentList(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void OutdentList()
        => ApplyEdit((t, s, l) => MarkdownEditing.OutdentList(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void QuoteToCallout()
        => ApplyEdit((t, s, l) => MarkdownEditing.QuoteToCallout(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void CalloutToQuote()
        => ApplyEdit((t, s, l) => MarkdownEditing.CalloutToQuote(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void CycleCalloutKind()
        => ApplyEdit((t, s, l) => MarkdownEditing.CycleCalloutKind(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void LinesToTaskList()
        => ApplyEdit((t, s, l) => MarkdownEditing.LinesToTaskList(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void TaskListToLines()
        => ApplyEdit((t, s, l) => MarkdownEditing.TaskListToLines(t, s, l) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void StripTrailingWhitespace()
        => ApplyEdit((t, _, _) => new EditResult(MarkdownEditing.StripTrailingWhitespace(t), 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void JoinLines() => ApplyEdit(MarkdownEditing.JoinLines);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ReflowParagraph() => ApplyEdit(MarkdownEditing.ReflowParagraph);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void Uppercase() => ApplyEdit((t, s, l) => MarkdownEditing.ChangeCase(t, s, l, upper: true));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void Lowercase() => ApplyEdit((t, s, l) => MarkdownEditing.ChangeCase(t, s, l, upper: false));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void TitleCase() => ApplyEdit(MarkdownEditing.TitleCase);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void SentenceCase() => ApplyEdit(MarkdownEditing.SentenceCase);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void Footnote() => ApplyEdit(MarkdownEditing.InsertFootnote);

    public bool TryExpandSnippet()
    {
        if (CurrentNote is null || host is null)
        {
            return false;
        }

        var (start, _) = host.GetSelection();
        var result = MarkdownSnippets.TryExpand(CurrentNote.Text, start, fileName: CurrentNote.FileName);
        if (result is null)
        {
            return false;
        }

        ApplyEdit((_, _, _) => result.Value);
        return true;
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public async Task CalloutAsync()
    {
        if (CurrentNote is not { } sourceTab) return;
        if (host is null)
        {
            ApplyEdit(sourceTab, (t, s, l) => MarkdownEditing.InsertCallout(t, s, l));
            return;
        }

        var kind = await host.PickAsync(
            Strings.T("CmdCallout.Label"),
            Strings.T("CmdCallout.Label"),
            ["info", "warning", "danger", "tip", "note", "abstract"]);
        if (kind is null) return;
        ApplyEdit(sourceTab, (t, s, l) => MarkdownEditing.InsertCallout(t, s, l, kind));
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task FrontMatterAsync() => EditFrontMatterAsync(null);

    public async Task EditFrontMatterAsync(string? key)
    {
        if (CurrentNote is not { } sourceTab) return;
        if (!FrontMatter.TrySplit(sourceTab.Text, out var fields, out _))
        {
            ApplyEdit(sourceTab, (_, _, _) => MarkdownEditing.InsertFrontMatter(sourceTab.Text));
            return;
        }

        if (host is null)
        {
            return;
        }

        key ??= await host.PromptAsync(Strings.T("FrontMatterKey"), "title");
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        fields.TryGetValue(key, out var current);
        var value = await host.PromptAsync(Strings.T("FrontMatterValue"), current ?? string.Empty);
        if (value is null)
        {
            return;
        }

        var next = FrontMatter.Upsert(sourceTab.Text, key.Trim(), value);
        ApplyEdit(sourceTab, (_, _, _) => new EditResult(next, 0, 0));
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public async Task InsertMermaidAsync()
    {
        if (CurrentNote is not { } sourceTab) return;
        if (host is null)
        {
            return;
        }

        var kind = await host.PickAsync(
            Strings.T("CmdMermaid.Label"),
            Strings.T("MermaidKind"),
            ["flowchart", "sequence", "pie", "state", "class", "gantt", "er", "plantuml", "usecase", "object", "deployment", "mindmap", "gitgraph", "timeline", "journey", "quadrant", "sankey", "xychart", "block", "timing", "requirement", "nwdiag", "c4", "architecture", "packet", "kanban", "radar", "salt", "treemap", "wbs", "ganttpuml", "jsonpuml", "yamlpuml", "mindmappuml", "ditaa", "regex", "ebnf", "archimate", "chen", "ie", "mathpuml", "latexpuml", "chronology", "sdl", "bpmn", "boardpuml", "gitpuml", "filespuml", "jcckit", "wireviz", "projectpuml", "dotpuml", "neato", "circo", "fdp", "twopi", "osage", "patchwork", "sfdp", "nop", "rack", "packetpuml", "c4puml", "elk", "smetana", "vizjs", "svek", "teoz", "picpuml", "creole", "eps", "umlet", "jlatexmath", "cute", "flowpuml", "defpuml", "mappuml", "listpuml", "networkpuml", "xmipuml", "scxmlpuml", "junglepuml", "infopuml", "entitypuml", "sudokupuml", "packagepuml", "folderpuml", "framepuml", "cloudpuml", "nodepuml", "queuepuml", "databasepuml", "rectanglepuml", "storagepuml", "cardpuml", "stackpuml", "artifactpuml", "hexagonpuml", "boundarypuml", "controlpuml", "interfacepuml", "actorpuml", "agentpuml", "labelpuml", "personpuml", "circlepuml", "collectionspuml", "togetherpuml", "notepuml", "boxpuml", "ovalpuml", "roundedpuml", "hiddenpuml", "partitionpuml", "grouppuml", "objectpuml", "treepuml", "legendpuml", "lanepuml", "swimpuml", "headerpuml", "footerpuml", "titlepuml", "captionpuml", "newpagepuml"]);
        if (kind is null) return;
        ApplyEdit(sourceTab, (t, s, l) => MarkdownEditing.InsertMermaid(t, s, l, kind));
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void CopyWikilink()
    {
        var stem = "note://" + CurrentNote!.NoteId;
        var heading = string.Empty;
        if (CurrentNote is not null)
        {
            var (start, _) = host?.GetSelection() ?? (0, 0);
            var map = new LineMap(CurrentNote.Text);
            var line = map.LineOfOffset(Math.Clamp(start, 0, CurrentNote.Text.Length));
            var raw = map.LineText(line);
            var level = MarkdownEditing.HeadingLevel(raw);
            if (level is >= 1 and <= 6)
            {
                heading = raw.TrimStart()[level..].Trim();
            }
        }

        _ = CopyToClipboardAsync(heading.Length == 0 ? "[[" + stem + "|" + CurrentNote!.Title + "]]" : "[[" + stem + "#" + heading + "|" + heading + "]]");
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void Typographer() => ApplyEdit((t, _, _) => MarkdownEditing.ApplyTypographer(t));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ToggleSlides()
    {
        if (CurrentNote is null)
        {
            return;
        }

        CurrentNote.SlideIndex = CurrentNote.SlideIndex < 0 ? 0 : -1;
        CurrentNote.KanbanMode = false;
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ToggleKanban()
    {
        if (CurrentNote is null)
        {
            return;
        }

        CurrentNote.KanbanMode = !CurrentNote.KanbanMode;
        if (CurrentNote.KanbanMode)
        {
            CurrentNote.SlideIndex = -1;
        }
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ToggleFocus()
    {
        if (CurrentNote is null)
        {
            return;
        }

        CurrentNote.FocusMode = !CurrentNote.FocusMode;
        if (CurrentNote.FocusMode)
        {
            CurrentNote.KanbanMode = false;
            CurrentNote.DiffMode = false;
            CurrentNote.DiffAgainstText = null;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSavedNote))]
    public async Task ToggleDiffAsync()
    {
        if (CurrentNote is null)
        {
            return;
        }

        var tab = CurrentNote;
        if (!tab.DiffMode)
        {
            var versions = await NoteArchive.Database.RevisionsAsync(tab.NoteId!);
            if (!ReferenceEquals(tab, CurrentNote)) return;
            tab.DiffAgainstText = versions.FirstOrDefault(r => r.Markdown != tab.Text)?.Markdown ?? tab.Text;
        }
        tab.DiffMode = !tab.DiffMode;
        if (CurrentNote.DiffMode)
        {
            CurrentNote.KanbanMode = false;
            CurrentNote.SlideIndex = -1;
        }
        else
        {
            CurrentNote.DiffAgainstText = null;
        }
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void CopyQuote()
    {
        if (CurrentNote is null)
        {
            return;
        }

        var (start, length) = host?.GetSelection() ?? (0, 0);
        start = Math.Clamp(start, 0, CurrentNote.Text.Length);
        length = Math.Clamp(length, 0, CurrentNote.Text.Length - start);
        var snippet = length > 0 ? CurrentNote.Text.Substring(start, length) : CurrentNote.Text;
        _ = CopyToClipboardAsync(MarkdownEditing.AsQuote(snippet));
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ConvertWikilinks()
        => ApplyEdit((text, _, _) => new EditResult(WikiLinks.ToMarkdownLinks(text), 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void ConvertMarkdownLinks()
        => ApplyEdit((text, _, _) => new EditResult(WikiLinks.ToWikilinks(text), 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void UnwrapWikilinks()
        => ApplyEdit((text, _, _) => new EditResult(WikiLinks.Unwrap(text), 0, 0));

    [RelayCommand]
    public void WrapDetails() => ApplyEdit((t, s, l) => MarkdownEditing.WrapDetails(t, s, l));

    [RelayCommand]
    public void UnwrapDetails() => ApplyEdit((t, s, l) => MarkdownEditing.UnwrapDetails(t, s) ?? new EditResult(t, s, l));

    [RelayCommand]
    public void NormalizeListMarkers() => ApplyEdit((t, s, l) => MarkdownEditing.NormalizeListMarkers(t));

    [RelayCommand]
    public void ToSetextHeading() => ApplyEdit((t, s, l) => MarkdownEditing.ToSetextHeading(t, s) ?? new EditResult(t, s, l));

    [RelayCommand]
    public void FromSetextHeading() => ApplyEdit((t, s, l) => MarkdownEditing.FromSetextHeading(t, s) ?? new EditResult(t, s, l));

    [RelayCommand]
    public void DuplicateHeading() => ApplyEdit((t, s, l) => MarkdownEditing.DuplicateHeadingSection(t, s) ?? new EditResult(t, s, l));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void InsertBlockId() => ApplyEdit((text, start, length) => MarkdownEditing.InsertBlockId(text, start, length));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void AssignMissingBlockIds()
        => ApplyEdit((text, _, _) => MarkdownEditing.AssignMissingBlockIds(text) ?? new EditResult(text, 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void StripBlockIds()
        => ApplyEdit((text, _, _) => MarkdownEditing.StripBlockIds(text) ?? new EditResult(text, 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void UnwrapFence()
        => ApplyEdit((text, start, length) => MarkdownEditing.UnwrapFence(text, start, length) ?? new EditResult(text, start, length));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public async Task SetFenceLanguageAsync()
    {
        if (CurrentNote is not { } sourceTab) return;
        if (host is null)
        {
            return;
        }

        var language = await host.PromptAsync(Strings.T("CmdSetFenceLanguage.Label"), "csharp");
        if (language is null)
        {
            return;
        }

        ApplyEdit(sourceTab, (text, start, _) => MarkdownEditing.SetFenceLanguage(text, start, language.Trim()) ?? new EditResult(text, start, 0));
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void StripHtmlComments()
        => ApplyEdit((text, _, _) => MarkdownEditing.StripHtmlComments(text) ?? new EditResult(text, 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void StampUpdated()
        => ApplyEdit((text, _, _) =>
        {
            var next = FrontMatter.StampUpdated(text, DateTimeOffset.Now);
            return new EditResult(next, 0, 0);
        });

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void NumberHeadings()
        => ApplyEdit((text, _, _) => MarkdownEditing.NumberHeadings(text));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void InsertBibliography()
        => ApplyEdit((text, _, _) => MarkdownCitations.Upsert(text, Strings.T("BibliographyHeading")));

    public void CycleKanbanCard(int line)
        => ApplyEdit((text, _, _) => MarkdownKanban.Toggle(text, line));

    public void MoveKanbanCard(int line, int delta)
        => ApplyEdit((text, _, _) => MarkdownKanban.Move(text, line, delta));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void NextSlide()
    {
        ShiftSlide(1);
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void PreviousSlide()
    {
        ShiftSlide(-1);
    }

    private void ShiftSlide(int delta)
    {
        if (CurrentNote is null)
        {
            return;
        }

        CurrentNote.Reparse();
        CurrentNote.KanbanMode = false;
        if (CurrentNote.Preview is null)
        {
            return;
        }

        var slides = ScrollMapper.Slides(CurrentNote.Preview.Blocks);
        if (slides.Count == 0)
        {
            return;
        }

        var index = CurrentNote.SlideIndex < 0 ? 0 : CurrentNote.SlideIndex;
        CurrentNote.SlideIndex = (index + delta + slides.Count) % slides.Count;
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void CopyMarkdown()
    {
        if (CurrentNote is null)
        {
            return;
        }

        _ = CopyToClipboardAsync(CurrentNote.Text);
    }

    [RelayCommand]
    public void ToggleImmersive() => host?.ToggleImmersive();

    [RelayCommand]
    public void ExitImmersive() => host?.SetImmersive(false);

    [RelayCommand]
    public void Share() => host?.Share();

    [RelayCommand]
    public Task ShowCommandPaletteAsync() => Views.CommandPaletteDialog.ShowForAsync(this);

    [RelayCommand]
    public async Task ShowQuickOpenAsync() { var note = await ArchiveDialogs.PickNoteAsync(); if (note is not null) await OpenNoteAsync(note.Id); }

    [RelayCommand]
    public void ShowGoToHeading() => host?.ShowGoToHeading();

    [RelayCommand]
    public void ToggleWordWrap()
        => _ = SettingsService.Instance.UpdateAsync(SettingsArea.Editor, s => s.Editor.WordWrap = !s.Editor.WordWrap);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void InsertTimestamp()
        => ApplyEdit((t, s, l) => MarkdownEditing.InsertTimestamp(t, s, l, DateTimeOffset.Now));

    public bool TryAutoPair(char opening)
    {
        if (CurrentNote is null || host is null)
        {
            return false;
        }

        var (start, length) = host.GetSelection();
        var result = MarkdownEditing.TryAutoPair(CurrentNote.Text, start, length, opening);
        if (result is not { } edit)
        {
            return false;
        }

        ApplyResult(CurrentNote, edit);
        return true;
    }

    [RelayCommand]
    public void SetViewMode(string mode)
    {
        if (CurrentNote is null)
        {
            return;
        }

        CurrentNote.ViewMode = mode switch
        {
            "Editor" => EditorViewMode.Editor,
            "Preview" => EditorViewMode.Preview,
            "SplitVertical" => EditorViewMode.SplitVertical,
            _ => EditorViewMode.Split
        };
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void SplitVertical() => SetViewMode("SplitVertical");

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void CopyHeadingLink()
    {
        if (CurrentNote is null || host is null)
        {
            return;
        }

        var (start, _) = host.GetSelection();
        var line = new LineMap(CurrentNote.Text).LineOfOffset(start);
        var node = CurrentNote.Outline.LastOrDefault(item => item.SourceLine <= line) ??
                   CurrentNote.Outline.FirstOrDefault();
        var title = node?.Title ?? Path.GetFileNameWithoutExtension(CurrentNote.FileName);
        var id = node?.Id ?? MarkdownEditing.HeadingAnchor(title);
        _ = CopyToClipboardAsync($"[{title}](#{id})");
    }

    public void SetTaskChecked(int sourceLine, bool isChecked)
    {
        if (CurrentNote is null)
        {
            return;
        }

        var result = MarkdownEditing.SetTaskChecked(CurrentNote.Text, sourceLine, isChecked);
        ApplyResult(CurrentNote, result);
    }

    private void NotifySelectionCommands()
    {
        // The palette owns the command catalog; only these two commands are UI-only.
        selectionCommands ??= EditorPalette.Commands(this).Select(item => item.Command)
            .Append(CloseEditorCommand).Append(FrontMatterCommand).Distinct().ToArray();
        foreach (var command in selectionCommands) command.NotifyCanExecuteChanged();
    }

    private bool HasNote() => CurrentNote is not null;

    private void NavigateToEditor()
    {
        // A Library start page can open a note before an EditorPage has attached its host.
        if (host is null && App.MainWindow is MainWindow window)
            window.NavigateToEditor();
        else
            host?.NavigateToEditor();
    }

}

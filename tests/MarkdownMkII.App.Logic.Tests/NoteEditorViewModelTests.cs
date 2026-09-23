using MarkdownMkII.Core.Text;
using MarkdownMkII.ViewModels;

namespace MarkdownMkII.App.Logic.Tests;

public sealed class NoteEditorViewModelTests
{
    [Fact]
    public void ClearingProtectedEditorRemovesTextParsedDataAndUndoRedo()
    {
        var tab = new NoteEditorViewModel();
        var summary = new MarkdownMkII.Storage.NoteSummary("secret-id", "Private", "", DateTimeOffset.UtcNow,
            true, MarkdownMkII.Storage.NoteColor.Red, "category", "PrivateCategory", ["PrivateTag"], false, false, true);
        tab.LoadArchiveNote(new(summary, "secret text", 1, DateTimeOffset.UtcNow));
        tab.SetTextFromEditor("secret edited", 13, 0);
        tab.Undo();
        tab.DiffAgainstText = "secret history";
        tab.FindQuery = "secret query";
        Assert.True(tab.CanRedo);
        tab.ClearSensitiveState();
        Assert.Empty(tab.Text);
        Assert.Empty(tab.Title);
        Assert.Null(tab.Parsed);
        Assert.Null(tab.Preview);
        Assert.Null(tab.Metadata);
        Assert.Null(tab.NoteResources);
        Assert.Null(tab.DiffAgainstText);
        Assert.Null(tab.FindQuery);
        Assert.False(tab.CanUndo);
        Assert.False(tab.CanRedo);
        Assert.Null(tab.Undo());
        Assert.Null(tab.Redo());
    }

    [Fact]
    public void MetadataChangesPreserveIdentityUnsavedTextAndUndoHistory()
    {
        var summary = new MarkdownMkII.Storage.NoteSummary("stable-id", "Before", "", DateTimeOffset.UtcNow,
            false, MarkdownMkII.Storage.NoteColor.None, null, null, [], false, false);
        var note = new NoteEditorViewModel();
        note.LoadArchiveNote(new(summary, "[Link](note://other-id)", 1, DateTimeOffset.UtcNow));
        note.SetTextFromEditor(note.Text + " edited", 30, 0);
        note.UpdateArchiveMetadata(summary with { Title = "Renamed", Favorite = true, Tags = ["work"] });
        Assert.Equal("stable-id", note.NoteId);
        Assert.Equal("Renamed", note.CardTitle);
        Assert.Equal("work", Assert.Single(note.CardTags));
        Assert.True(note.IsDirty);
        Assert.Contains("edited", note.Text);
        note.Undo();
        Assert.Equal("[Link](note://other-id)", note.Text);
        Assert.False(note.IsDirty);
    }


    [Fact]
    public void SwitchingPresentationModesActivatesTheRequestedMode()
    {
        var tab = new NoteEditorViewModel { DiffMode = true, DiffAgainstText = "Old" };
        tab.SlideIndex = 0;
        Assert.False(tab.DiffMode);
        Assert.Null(tab.DiffAgainstText);
        tab.KanbanMode = true;
        Assert.Equal(-1, tab.SlideIndex);
        tab.DiffMode = true;
        Assert.False(tab.KanbanMode);
        Assert.Equal(-1, tab.SlideIndex);
    }

    [Fact]
    public void ReplacingComparisonTextRefreshesAnAlreadyVisibleDiff()
    {
        var tab = new NoteEditorViewModel { DiffMode = true, DiffAgainstText = "Before" };
        var changed = new List<string?>();
        tab.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        tab.DiffAgainstText = "Another revision";
        Assert.Contains(nameof(tab.DiffMode), changed);
    }

    [Fact]
    public void InitialDocumentLoadIsNotAnUndoableEdit()
    {
        var tab = new NoteEditorViewModel { Text = "Existing document" };
        tab.Reparse();
        Assert.False(tab.CanUndo);
    }

    [Fact]
    public void UndoAndRedoPreserveSavedStateAndSelection()
    {
        var tab = new NoteEditorViewModel { Text = "A document" };
        tab.Reparse();
        tab.UpdateSelection(2, 8);
        tab.ApplyEdit(new EditResult("A note", 6, 0));

        var undone = tab.Undo()!.Value;

        Assert.Equal("A document", tab.Text);
        Assert.Equal(2, undone.SelectionStart);
        Assert.Equal(8, undone.SelectionLength);
        Assert.False(tab.IsDirty);
        Assert.True(tab.CanRedo);
        tab.Redo();
        Assert.Equal("A note", tab.Text);
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void UndoAcrossSaveKeepsHistoryAndTracksSavedText()
    {
        var tab = new NoteEditorViewModel { Text = "Before" };
        tab.Reparse();
        tab.SetTextFromEditor("After", 5, 0);
        tab.CompleteArchiveSave(tab.CaptureSaveSnapshot(), 2);

        tab.Undo();
        Assert.Equal("Before", tab.Text);
        Assert.True(tab.IsDirty);
        tab.Redo();
        Assert.Equal("After", tab.Text);
        Assert.False(tab.IsDirty);
    }

    [Fact]
    public void ReopenedNotesHaveIndependentUndoHistory()
    {
        var first = new NoteEditorViewModel { Text = "First" };
        var second = new NoteEditorViewModel { Text = "Second" };
        first.Reparse();
        second.Reparse();
        first.SetTextFromEditor("Edited first", 12, 0);
        second.SetTextFromEditor("Edited second", 13, 0);
        first.Undo();
        Assert.Equal("First", first.Text);
        Assert.Equal("Edited second", second.Text);
        Assert.True(second.CanUndo);
    }

    [Fact]
    public void NoOpFormattingKeepsCleanDocumentAndParsedSnapshot()
    {
        var tab = new NoteEditorViewModel { Text = "# Title\n\nBody\n" };
        tab.Reparse();
        var parsed = tab.Parsed;

        tab.ApplyEdit(new EditResult(tab.Text, 3, 2));

        Assert.False(tab.IsDirty);
        Assert.Same(parsed, tab.Parsed);
        Assert.Equal(3, tab.CaretStart);
    }

    [Fact]
    public void NoOpDoesNotClearExistingUnsavedChanges()
    {
        var tab = new NoteEditorViewModel { Text = "Unsaved", IsDirty = true };
        tab.ApplyEdit(new EditResult(tab.Text, 0, 0));
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public void EditsRefreshOutlineAndClampSelectionToNewDocument()
    {
        var tab = new NoteEditorViewModel { Text = "# Old title\n" };
        tab.Reparse();

        var result = tab.ApplyEdit(new EditResult("# New\n", int.MaxValue, int.MaxValue));

        Assert.True(tab.IsDirty);
        Assert.Equal("New", Assert.Single(tab.Outline).Title);
        Assert.Equal(6, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
        Assert.Equal(result.SelectionStart, tab.CaretStart);
    }

    [Fact]
    public void NegativeSelectionIsClampedWithoutChangingText()
    {
        var tab = new NoteEditorViewModel { Text = "Note" };
        var result = tab.ApplyEdit(new EditResult(tab.Text, -1, -1));
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
        Assert.False(tab.IsDirty);
    }

    [Fact]
    public void CompletingSaveRetainsTypingThatArrivedDuringWrite()
    {
        var tab = new NoteEditorViewModel { Text = "Saved version", IsDirty = true };
        var snapshot = tab.CaptureSaveSnapshot();
        tab.Text = "New typing";

        tab.CompleteArchiveSave(snapshot, 2);

        Assert.True(tab.IsDirty);
        Assert.Equal("New typing", tab.Text);
        Assert.Equal(2, tab.Version);
    }

    [Fact]
    public void CompletingUnchangedSaveClearsDirtyFlag()
    {
        var tab = new NoteEditorViewModel { Text = "Saved version", IsDirty = true };
        tab.CompleteArchiveSave(tab.CaptureSaveSnapshot(), 2);
        Assert.False(tab.IsDirty);
    }

    [Fact]
    public void ParsedSnapshotNotifiesPreviewOutlineAndCardBindings()
    {
        var tab = new NoteEditorViewModel { Text = "# A title\n" };
        var changed = new List<string?>();
        tab.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        tab.Reparse();

        Assert.Contains(nameof(tab.Preview), changed);
        Assert.Contains(nameof(tab.Outline), changed);
        Assert.Contains(nameof(tab.CardTitle), changed);
        Assert.Equal("A title", tab.CardTitle);
    }
}

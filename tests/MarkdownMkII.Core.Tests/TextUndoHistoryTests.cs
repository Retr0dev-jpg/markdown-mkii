using MarkdownMkII.Core.Editor;

namespace MarkdownMkII.Core.Tests;

public sealed class TextUndoHistoryTests
{
    [Fact]
    public void UndoRedoRestoresReplacementAndBothSelections()
    {
        var history = new TextUndoHistory();
        history.Record("hello world", "hello reader", 6, 5, 12, 0);
        var undo = history.Undo("hello reader")!.Value;
        Assert.Equal("hello world", undo.Text);
        Assert.Equal(6, undo.SelectionStart);
        Assert.Equal(5, undo.SelectionLength);
        var redo = history.Redo(undo.Text)!.Value;
        Assert.Equal("hello reader", redo.Text);
        Assert.Equal(12, redo.SelectionStart);
        Assert.Equal(0, redo.SelectionLength);
    }

    [Fact]
    public void TypingAfterUndoDiscardsOnlyRedoBranch()
    {
        var history = new TextUndoHistory();
        history.Record("", "a", 0, 0, 1, 0);
        history.Record("a", "ab", 1, 0, 2, 0);
        history.Undo("ab");
        history.Record("a", "ac", 1, 0, 2, 0);
        Assert.False(history.CanRedo);
        Assert.Equal("a", history.Undo("ac")!.Value.Text);
        Assert.Equal("", history.Undo("a")!.Value.Text);
    }

    [Fact]
    public void NoOpDoesNotDiscardRedo()
    {
        var history = new TextUndoHistory();
        history.Record("a", "ab", 1, 0, 2, 0);
        history.Undo("ab");
        history.Record("a", "a", 0, 0, 1, 0);
        Assert.True(history.CanRedo);
    }

    [Fact]
    public void EntryLimitKeepsRecentChainUsable()
    {
        var history = new TextUndoHistory(maxEntries: 2);
        history.Record("", "a", 0, 0, 1, 0);
        history.Record("a", "ab", 1, 0, 2, 0);
        history.Record("ab", "abc", 2, 0, 3, 0);
        Assert.Equal("ab", history.Undo("abc")!.Value.Text);
        Assert.Equal("a", history.Undo("ab")!.Value.Text);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void SmallChangesToLargeDocumentsUseChangedTextBudget()
    {
        var original = new string('x', 100_000);
        var history = new TextUndoHistory(maxBytes: 100);
        history.Record(original, original + "a", 0, 0, original.Length + 1, 0);
        Assert.Equal(original, history.Undo(original + "a")!.Value.Text);
    }

    [Fact]
    public void OversizedReplacementInvalidatesOlderChain()
    {
        var history = new TextUndoHistory(maxBytes: 100);
        history.Record("a", "ab", 1, 0, 2, 0);
        history.Record("ab", new string('x', 100), 2, 0, 100, 0);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UnexpectedCurrentContentIsNeverOverwritten()
    {
        var history = new TextUndoHistory();
        history.Record("before", "after", 0, 0, 0, 0);
        Assert.Null(history.Undo("other"));
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void TypingIsUndoneOneWordAtATime()
    {
        var history = new TextUndoHistory();
        var text = "";
        foreach (var c in "hello world")
        {
            var next = text + c;
            history.Record(text, next, text.Length, 0, next.Length, 0, coalesce: true);
            text = next;
        }

        Assert.Equal("hello ", history.Undo(text)!.Value.Text);
        Assert.Equal("", history.Undo("hello ")!.Value.Text);
        Assert.False(history.CanUndo);
        Assert.Equal("hello ", history.Redo("")!.Value.Text);
    }

    [Fact]
    public void BackspacesAndCommandsStaySeparateSteps()
    {
        var history = new TextUndoHistory();
        history.Record("abc", "abcd", 3, 0, 4, 0, coalesce: true);
        history.Record("abcd", "abc", 4, 0, 3, 0, coalesce: true);
        history.Record("abc", "ab", 3, 0, 2, 0, coalesce: true);
        history.Record("ab", "**ab**", 0, 2, 2, 2);
        Assert.Equal("ab", history.Undo("**ab**")!.Value.Text);
        Assert.Equal("abcd", history.Undo("ab")!.Value.Text);
        Assert.Equal("abc", history.Undo("abcd")!.Value.Text);
    }

    [Fact]
    public void UnicodeAndLineEndingsRoundTrip()
    {
        var history = new TextUndoHistory();
        history.Record("A😀\r\nB", "A😇\nB", 1, 2, 3, 0);
        Assert.Equal("A😀\r\nB", history.Undo("A😇\nB")!.Value.Text);
        Assert.Equal("A😇\nB", history.Redo("A😀\r\nB")!.Value.Text);
    }
}

using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Editor;

/// <summary>Per-document undo history storing only the changed portions of text.</summary>
public sealed class TextUndoHistory(int maxEntries = 500, long maxBytes = 8 * 1024 * 1024)
{
    private readonly LinkedList<Change> undo = [];
    private readonly Stack<Change> redo = [];
    private long retainedBytes;

    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    public void Clear()
    {
        undo.Clear();
        redo.Clear();
        retainedBytes = 0;
    }

    /// <param name="coalesce">
    /// Typing: merge with the previous typed step while characters are inserted or deleted contiguously,
    /// so undo removes a word instead of a single character.
    /// </param>
    public void Record(string before, string after, int beforeStart, int beforeLength, int afterStart, int afterLength, bool coalesce = false)
    {
        if (string.Equals(before, after, StringComparison.Ordinal)) return;

        var hadRedo = redo.Count > 0;
        foreach (var discarded in redo) retainedBytes -= discarded.Bytes;
        redo.Clear();

        var prefix = 0;
        var commonLength = Math.Min(before.Length, after.Length);
        while (prefix < commonLength && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < commonLength - prefix && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;

        var change = new Change(prefix, before.Substring(prefix, before.Length - prefix - suffix),
            after.Substring(prefix, after.Length - prefix - suffix), before.Length, after.Length,
            beforeStart, beforeLength, afterStart, afterLength, coalesce);
        if (maxEntries <= 0 || change.Bytes > maxBytes)
        {
            // Skipping a large edit must also discard the older, now incompatible chain.
            Clear();
            return;
        }

        if (coalesce && !hadRedo && undo.Last is { Value.Typing: true } last && Merge(last.Value, change) is { } merged)
        {
            retainedBytes += merged.Bytes - last.Value.Bytes;
            last.Value = merged;
            return;
        }

        undo.AddLast(change);
        retainedBytes += change.Bytes;
        while (undo.Count > maxEntries || retainedBytes > maxBytes)
        {
            retainedBytes -= undo.First!.Value.Bytes;
            undo.RemoveFirst();
        }
    }

    public EditResult? Undo(string text)
    {
        if (undo.Last is not { } last) return null;
        var change = last.Value;
        if (!Matches(text, change.Offset, change.Inserted, change.AfterLength))
        {
            Clear();
            return null;
        }

        undo.RemoveLast();
        redo.Push(change);
        return Apply(text, change.Offset, change.Inserted.Length, change.Removed, change.BeforeStart, change.BeforeSelectionLength);
    }

    public EditResult? Redo(string text)
    {
        if (!redo.TryPeek(out var change)) return null;
        if (!Matches(text, change.Offset, change.Removed, change.BeforeLength))
        {
            Clear();
            return null;
        }

        redo.Pop();
        undo.AddLast(change);
        return Apply(text, change.Offset, change.Removed.Length, change.Inserted, change.AfterStart, change.AfterSelectionLength);
    }

    private static bool Matches(string text, int offset, string expected, int expectedLength)
        => text.Length == expectedLength && offset <= text.Length - expected.Length &&
           text.AsSpan(offset, expected.Length).SequenceEqual(expected.AsSpan());

    private static EditResult Apply(string text, int offset, int length, string replacement, int start, int selectionLength)
    {
        var result = string.Concat(text.AsSpan(0, offset), replacement.AsSpan(), text.AsSpan(offset + length));
        start = Math.Clamp(start, 0, result.Length);
        return new EditResult(result, start, Math.Clamp(selectionLength, 0, result.Length - start));
    }

    private static Change? Merge(Change last, Change next)
    {
        if (last.AfterLength != next.BeforeLength) return null;
        if (last.Removed.Length == 0 && next.Removed.Length == 0 && next.Inserted.Length == 1 &&
            last.Offset + last.Inserted.Length == next.Offset)
        {
            var typed = next.Inserted[0];
            var previous = last.Inserted[^1];
            if (typed is '\n' or '\r' || previous is '\n' || (char.IsWhiteSpace(previous) && !char.IsWhiteSpace(typed))) return null;
            return last with
            {
                Inserted = last.Inserted + next.Inserted, AfterLength = next.AfterLength,
                AfterStart = next.AfterStart, AfterSelectionLength = next.AfterSelectionLength
            };
        }

        if (last.Inserted.Length == 0 && next.Inserted.Length == 0 && next.Removed.Length == 1 && next.Removed[0] != '\n')
        {
            // Backspace moves left through the text, Delete keeps the same offset.
            if (next.Offset + 1 == last.Offset)
                return last with { Offset = next.Offset, Removed = next.Removed + last.Removed, AfterLength = next.AfterLength, AfterStart = next.AfterStart, AfterSelectionLength = next.AfterSelectionLength };
            if (next.Offset == last.Offset)
                return last with { Removed = last.Removed + next.Removed, AfterLength = next.AfterLength, AfterStart = next.AfterStart, AfterSelectionLength = next.AfterSelectionLength };
        }

        return null;
    }

    private sealed record Change(int Offset, string Removed, string Inserted, int BeforeLength, int AfterLength,
        int BeforeStart, int BeforeSelectionLength, int AfterStart, int AfterSelectionLength, bool Typing = false)
    {
        public long Bytes => ((long)Removed.Length + Inserted.Length) * sizeof(char) + 64;
    }
}

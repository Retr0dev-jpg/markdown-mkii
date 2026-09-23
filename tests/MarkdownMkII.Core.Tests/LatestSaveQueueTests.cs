using MarkdownMkII.Core.Editor;

namespace MarkdownMkII.Core.Tests;

public sealed class LatestSaveQueueTests
{
    [Fact]
    public async Task DrainsEditsArrivingDuringASlowWriteAndSerializesClose()
    {
        var queue = new LatestSaveQueue<string>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var text = "first";
        var saved = "";
        var writes = new List<string>();
        string? Capture(bool force) => force || text != saved ? text : null;
        async Task Persist(string snapshot, bool checkpoint)
        {
            writes.Add(snapshot);
            if (snapshot == "first") { started.TrySetResult(); await release.Task; }
            saved = snapshot;
        }
        var save = queue.FlushAsync(Capture, Persist);
        await started.Task;
        text = "last keystroke";
        var close = queue.FlushAsync(Capture, Persist, checkpoint: true);
        Assert.False(close.IsCompleted);
        release.SetResult();
        await Task.WhenAll(save, close);
        Assert.Equal(text, saved);
        Assert.Equal(new[] { "first", "last keystroke", "last keystroke" }, writes);
        Assert.False(queue.IsSaving);
    }

    [Fact]
    public async Task FailureLeavesTheLatestSnapshotAvailableForRetry()
    {
        var queue = new LatestSaveQueue<string>();
        var text = "unsaved";
        string? saved = null;
        string? Capture(bool force) => text != saved ? text : null;
        await Assert.ThrowsAsync<IOException>(() => queue.FlushAsync(Capture, (_, _) => throw new IOException("Disk full")));
        Assert.False(queue.IsSaving);
        Assert.Null(saved);
        text += " and still editable";
        await queue.FlushAsync(Capture, (snapshot, _) => { saved = snapshot; return Task.CompletedTask; });
        Assert.Equal(text, saved);
    }
}

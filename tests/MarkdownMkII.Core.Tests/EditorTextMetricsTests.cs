using System.Diagnostics;
using MarkdownMkII.Core.Editor;
using MarkdownMkII.Core.Text;
using Xunit.Abstractions;

namespace MarkdownMkII.Core.Tests;

public sealed class EditorTextMetricsTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(16384)]
    [InlineData(1048576)]
    [InlineData(5242880)]
    public async Task CaretMovesReuseMapsAndTotals(int size)
    {
        const string paragraph = "# Titolo\nCaffè, parole e selezione nell’editor.\n";
        var text = string.Concat(Enumerable.Repeat(paragraph, size / paragraph.Length));
        var oldBytes = GC.GetAllocatedBytesForCurrentThread();
        var old = Stopwatch.StartNew();
        for (var i = 0; i < 12; i++)
        {
            _ = WordStats.Compute(text, i * 5, 20);
            for (var j = 0; j < 3; j++) _ = new LineMap(text).LineOfOffset(i * 5);
        }
        old.Stop(); oldBytes = GC.GetAllocatedBytesForCurrentThread() - oldBytes;
        var metrics = new EditorTextMetrics(text);
        var map = metrics.Lines;
        var totals = await metrics.Statistics;
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var current = Stopwatch.StartNew();
        for (var i = 0; i < 12; i++)
        {
            Assert.Same(map, metrics.Lines);
            var stats = await metrics.SelectionAsync(i * 5, 20);
            Assert.Equal(totals.Words, stats.Words);
            Assert.Equal(WordStats.CountWords(text.Substring(i * 5, 20)), stats.SelectedWords);
        }
        current.Stop(); bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        output.WriteLine($"{size} characters, 12 selection moves: previous={old.Elapsed.TotalMilliseconds:F2}ms/{oldBytes}B; cached={current.Elapsed.TotalMilliseconds:F2}ms/{bytes}B (cache construction excluded). ");
    }
    [Fact]
    public async Task SelectionHandlesBoundsUnicodeAndCancellation()
    {
        var metrics = new EditorTextMetrics("Caffè e l’acqua\nseconda riga");
        var selected = await metrics.SelectionAsync(0, 999);
        Assert.Equal(selected.Words, selected.SelectedWords);
        Assert.Equal(0, (await metrics.SelectionAsync(999, 50)).SelectedWords);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => metrics.SelectionAsync(0, 5, cancellation.Token));
    }
}

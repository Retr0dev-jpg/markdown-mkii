using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Editor;

/// <summary>One immutable text revision shared by caret, gutter and status calculations.</summary>
public sealed class EditorTextMetrics(string text)
{
    public string Text { get; } = text;
    private LineMap? lines;
    private Task<DocumentStats>? statistics;
    public LineMap Lines => lines ??= new(Text);
    public Task<DocumentStats> Statistics => statistics ??= Task.Run(() => WordStats.Compute(Text));
    public async Task<DocumentStats> SelectionAsync(int start, int length, CancellationToken token = default)
    {
        var totals = await Statistics.WaitAsync(token).ConfigureAwait(false);
        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        token.ThrowIfCancellationRequested();
        var words = length == 0 ? 0 : length < 8192 ? WordStats.CountWords(Text.AsSpan(start, length)) :
            await Task.Run(() => { token.ThrowIfCancellationRequested(); return WordStats.CountWords(Text.AsSpan(start, length)); }, token).ConfigureAwait(false);
        return totals with { SelectedCharacters = length, SelectedWords = words };
    }
}

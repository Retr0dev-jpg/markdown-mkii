namespace MarkdownMkII.Core.Text;

/// <summary>Tracks the marker and minimum closing length of a Markdown code fence.</summary>
internal sealed class MarkdownFence
{
    private char marker;
    private int length;

    public bool IsOpen => length > 0;

    public static bool[] CodeLines(LineMap map)
    {
        var lines = new bool[map.LineCount];
        var state = new MarkdownFence();
        for (var i = 0; i < lines.Length; i++)
        {
            var inside = state.IsOpen;
            lines[i] = state.Advance(map.LineText(i)) || inside;
        }

        return lines;
    }

    /// <returns>True when this line opens or closes the active fence.</returns>
    public bool Advance(string line)
    {
        var start = 0;
        while (start < line.Length && start < 4 && line[start] == ' ')
        {
            start++;
        }

        if (start > 3 || start == line.Length || line[start] is not ('`' or '~'))
        {
            return false;
        }

        var candidate = line[start];
        var end = start;
        while (end < line.Length && line[end] == candidate)
        {
            end++;
        }

        var count = end - start;
        if (IsOpen)
        {
            if (candidate != marker || count < length || !string.IsNullOrWhiteSpace(line[end..]))
            {
                return false;
            }

            length = 0;
            return true;
        }

        if (count < 3 || candidate == '`' && line.AsSpan(end).Contains('`'))
        {
            return false;
        }

        marker = candidate;
        length = count;
        return true;
    }
}

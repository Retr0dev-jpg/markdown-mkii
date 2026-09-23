namespace MarkdownMkII.Core.Text;

/// <summary>
/// Indici di inizio riga nel testo sorgente (0-based, stile Markdig).
/// Permette di convertire riga ↔ offset senza ricalcolare ogni volta.
/// </summary>
public sealed class LineMap
{
    private readonly int[] starts;
    private readonly string text;

    public LineMap(string text)
    {
        this.text = text ?? string.Empty;
        starts = Build(this.text);
    }

    public string Text => text;

    public int LineCount => starts.Length;

    public int OffsetOfLine(int line)
    {
        if (line < 0)
        {
            return 0;
        }

        if (line >= starts.Length)
        {
            return text.Length;
        }

        return starts[line];
    }

    public int LineOfOffset(int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var index = Array.BinarySearch(starts, offset);
        return index >= 0 ? index : ~index - 1;
    }

    public string LineText(int line)
    {
        if (line < 0 || line >= starts.Length)
        {
            return string.Empty;
        }

        var start = starts[line];
        var end = line + 1 < starts.Length ? starts[line + 1] : text.Length;
        if (end > start && text[end - 1] is '\r' or '\n')
        {
            var newline = text[end - 1];
            end--;
            if (newline == '\n' && end > start && text[end - 1] == '\r')
            {
                end--;
            }
        }

        return text[start..end];
    }

    public (int Start, int Length) LineSpan(int line)
    {
        if (line < 0 || line >= starts.Length)
        {
            return (OffsetOfLine(line), 0);
        }

        var start = OffsetOfLine(line);
        var next = OffsetOfLine(line + 1);
        return (start, Math.Max(0, next - start));
    }

    private static int[] Build(string value)
    {
        var list = new List<int> { 0 };
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is not ('\r' or '\n'))
            {
                continue;
            }

            if (value[i] == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
            {
                i++;
            }

            list.Add(i + 1);
        }

        return [.. list];
    }
}

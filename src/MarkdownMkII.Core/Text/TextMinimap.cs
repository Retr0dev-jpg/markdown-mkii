namespace MarkdownMkII.Core.Text;

public sealed record MinimapMark(double Top, double Height, double Density, int Line, bool Current);

public static class TextMinimap
{
    public static IReadOnlyList<MinimapMark> Plan(
        string text,
        double height,
        int currentLine = 0,
        int maxMarks = 240)
    {
        if (string.IsNullOrEmpty(text) || height <= 0)
        {
            return [];
        }

        var map = new LineMap(text);
        if (map.LineCount <= 0)
        {
            return [];
        }

        maxMarks = Math.Clamp(maxMarks, 8, 400);
        var step = Math.Max(1, (int)Math.Ceiling(map.LineCount / (double)maxMarks));
        var lineHeight = height / map.LineCount;
        var marks = new List<MinimapMark>((map.LineCount / step) + 1);
        for (var line = 0; line < map.LineCount; line += step)
        {
            var density = 0d;
            var count = 0;
            for (var i = line; i < line + step && i < map.LineCount; i++)
            {
                density += Math.Clamp(map.LineText(i).Trim().Length / 96.0, 0, 1);
                count++;
            }

            density = count == 0 ? 0 : density / count;
            var current = currentLine >= line && currentLine < line + step;
            marks.Add(new MinimapMark(line * lineHeight, Math.Max(1, lineHeight * step), density, line, current));
        }

        return marks;
    }
}

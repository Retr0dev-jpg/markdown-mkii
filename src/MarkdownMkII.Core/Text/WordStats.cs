using System.Globalization;
using System.Text.RegularExpressions;

namespace MarkdownMkII.Core.Text;

public readonly record struct DocumentStats(
    int Characters,
    int CharactersNoSpaces,
    int Words,
    int Lines,
    int SelectedCharacters,
    int SelectedWords,
    TimeSpan ReadingTime);

public static class WordStats
{
    private static readonly Regex WordPattern = new(@"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*", RegexOptions.Compiled);

    /// <summary>Parole al minuto usate per il tempo di lettura (italiano/inglese, valore medio).</summary>
    public const int WordsPerMinute = 220;

    public static DocumentStats Compute(string text, int selectionStart = 0, int selectionLength = 0)
    {
        text ??= string.Empty;
        var characters = text.Length;
        var charactersNoSpaces = CountNonWhitespace(text);
        var words = CountWords(text);
        var lines = text.Length == 0 ? 0 : CountLines(text);

        var selected = Slice(text, selectionStart, selectionLength);
        var selectedCharacters = selected.Length;
        var selectedWords = selected.Length == 0 ? 0 : CountWords(selected);

        var minutes = words == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(words / (double)WordsPerMinute));
        return new DocumentStats(
            characters,
            charactersNoSpaces,
            words,
            lines,
            selectedCharacters,
            selectedWords,
            TimeSpan.FromMinutes(minutes));
    }

    public static int CountWords(ReadOnlySpan<char> text) => WordPattern.Count(text);

    public static int CountWords(string text) => WordPattern.Count(text ?? string.Empty);

    private static int CountNonWhitespace(string text)
    {
        var count = 0;
        foreach (var ch in text)
        {
            if (!char.IsWhiteSpace(ch))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountLines(string text)
    {
        var lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                lines++;
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (text[i] == '\n')
            {
                lines++;
            }
        }

        return lines;
    }

    private static string Slice(string text, int start, int length)
    {
        if (length <= 0 || text.Length == 0)
        {
            return string.Empty;
        }

        start = Math.Clamp(start, 0, text.Length);
        var end = start + Math.Clamp(length, 0, text.Length - start);
        return start >= end ? string.Empty : text[start..end];
    }

    public static string FormatReadingTime(TimeSpan time, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var minutes = Math.Max(1, (int)Math.Ceiling(time.TotalMinutes));
        return minutes.ToString(culture);
    }
}

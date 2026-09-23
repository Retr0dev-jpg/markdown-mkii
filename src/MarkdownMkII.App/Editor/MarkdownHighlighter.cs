using MarkdownMkII.Core.Highlight;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MarkdownMkII.Editor;

public sealed class MarkdownHighlighter
{
    public static readonly Color Heading = Color.FromArgb(255, 0, 120, 212);
    public static readonly Color Strong = Color.FromArgb(255, 196, 43, 28);
    public static readonly Color Emphasis = Color.FromArgb(255, 136, 23, 152);
    public static readonly Color Code = Color.FromArgb(255, 13, 128, 84);
    public static readonly Color Link = Color.FromArgb(255, 0, 120, 212);
    public static readonly Color Quote = Color.FromArgb(255, 118, 118, 118);
    public static readonly Color FrontMatter = Color.FromArgb(255, 154, 103, 0);

    public bool IsApplying { get; private set; }

    public void Apply(
        RichEditBox box,
        string text,
        bool largeFile,
        Markdig.Syntax.MarkdownDocument? syntax = null,
        IReadOnlyList<MarkdownMkII.Core.Text.FindMatch>? findMatches = null,
        IReadOnlyList<(int Start, int Length)>? hiddenRanges = null)
    {
        if (box.Document is null)
        {
            return;
        }

        IsApplying = true;
        var document = box.Document;
        var undoLimit = document.UndoLimit;
        try
        {
            document.UndoLimit = 0;
            document.BatchDisplayUpdates();
            var isDark = box.ActualTheme == ElementTheme.Dark
                || (box.ActualTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);
            var range = document.GetRange(0, Math.Max(0, text.Length));
            // Read resolved control brushes so explicit and system themes use the same native colors.
            if (box.Foreground is SolidColorBrush foreground)
                range.CharacterFormat.ForegroundColor = foreground.Color;
            // RichEdit ignores alpha for character backgrounds; use the opaque native editor surface.
            if (box.Background is SolidColorBrush background)
                range.CharacterFormat.BackgroundColor = background.Color;
            range.CharacterFormat.Hidden = FormatEffect.Off;

            var spans = syntax is null
                ? MarkdownSpanClassifier.Classify(text, largeFile)
                : MarkdownSpanClassifier.Classify(syntax, text, largeFile);
            foreach (var span in spans)
            {
                if (span.Start < 0 || span.Length <= 0)
                {
                    continue;
                }

                var start = Math.Clamp(span.Start, 0, text.Length);
                var end = Math.Clamp(span.Start + span.Length, start, text.Length);
                if (end <= start)
                {
                    continue;
                }

                var colored = document.GetRange(start, end);
                colored.CharacterFormat.ForegroundColor = ColorFor(span.Kind, isDark);
            }

            if (findMatches is { Count: > 0 })
            {
                var highlight = isDark ? Color.FromArgb(255, 180, 140, 24) : Color.FromArgb(255, 255, 213, 79);
                var limit = Math.Min(findMatches.Count, 200);
                for (var i = 0; i < limit; i++)
                {
                    var match = findMatches[i];
                    var start = Math.Clamp(match.Start, 0, text.Length);
                    var end = Math.Clamp(match.Start + match.Length, start, text.Length);
                    if (end <= start)
                    {
                        continue;
                    }

                    var marked = document.GetRange(start, end);
                    marked.CharacterFormat.BackgroundColor = highlight;
                }
            }

            if (hiddenRanges is { Count: > 0 })
            {
                foreach (var hidden in hiddenRanges)
                {
                    if (hidden.Length <= 0)
                    {
                        continue;
                    }

                    var start = Math.Clamp(hidden.Start, 0, text.Length);
                    var end = Math.Clamp(hidden.Start + hidden.Length, start, text.Length);
                    if (end <= start)
                    {
                        continue;
                    }

                    var folded = document.GetRange(start, end);
                    folded.CharacterFormat.Hidden = FormatEffect.On;
                }
            }
        }
        finally
        {
            try
            {
                document.ApplyDisplayUpdates();
            }
            catch
            {
            }

            try
            {
                document.UndoLimit = undoLimit;
            }
            catch
            {
            }

            IsApplying = false;
        }
    }

    private static Color ColorFor(MarkdownSpanKind kind, bool dark) => kind switch
    {
        MarkdownSpanKind.Heading => dark ? Color.FromArgb(255, 78, 201, 176) : Heading,
        MarkdownSpanKind.Strong => dark ? Color.FromArgb(255, 224, 108, 117) : Strong,
        MarkdownSpanKind.Emphasis or MarkdownSpanKind.Strikethrough => dark ? Color.FromArgb(255, 197, 134, 192) : Emphasis,
        MarkdownSpanKind.Code or MarkdownSpanKind.CodeFence => dark ? Color.FromArgb(255, 152, 195, 121) : Code,
        MarkdownSpanKind.Link or MarkdownSpanKind.Image or MarkdownSpanKind.WikiLink => dark ? Color.FromArgb(255, 86, 156, 214) : Link,
        MarkdownSpanKind.Quote or MarkdownSpanKind.ListMarker or MarkdownSpanKind.TablePipe => dark ? Color.FromArgb(255, 153, 153, 153) : Quote,
        MarkdownSpanKind.FrontMatter or MarkdownSpanKind.Html or MarkdownSpanKind.Math => dark ? Color.FromArgb(255, 209, 154, 102) : FrontMatter,
        _ => dark ? Color.FromArgb(255, 153, 153, 153) : Quote
    };
}

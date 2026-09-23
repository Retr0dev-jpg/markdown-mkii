using MarkdownMkII.Storage;
using MarkdownMkII.Services.Localization;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace MarkdownMkII.ViewModels;
public sealed class ArchiveNoteCard(NoteSummary note) : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public NoteSummary Note => note;
    public void Update(NoteSummary value)
    {
        if (note == value || (note with { Tags = value.Tags }) == value && note.Tags.SequenceEqual(value.Tags)) return;
        note = value;
        PropertyChanged?.Invoke(this, new(null));
    }
    public string Title => string.IsNullOrEmpty(note.Title) && note.IsProtected ? Strings.T("SecurityProtectedNote") : note.Title;
    public string Description => string.Join(" · ", new[] { note.Category, note.Tags.Count > 0 ? string.Join(" ", note.Tags.Take(2).Select(t => "#" + t)) : null, note.Preview }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public bool IsFavorite => note.Favorite;
    public bool IsProtected => note.IsProtected;
    public string ProtectionGlyph => note.IsLocked ? "\uE72E" : "\uE785";
    public string ModifiedLabel
    {
        get
        {
            var modified = note.Modified.ToLocalTime();
            var elapsed = DateTimeOffset.Now - modified;
            if (elapsed < TimeSpan.FromMinutes(1)) return Strings.T("NoteModifiedNow");
            if (elapsed < TimeSpan.FromHours(1)) return Strings.Format(Strings.DefaultMap, "NoteModifiedMinutes", (int)elapsed.TotalMinutes);
            if (modified.Date == DateTime.Today) return Strings.Format(Strings.DefaultMap, "NoteModifiedHours", (int)elapsed.TotalHours);
            if (modified.Date == DateTime.Today.AddDays(-1)) return Strings.T("NoteModifiedYesterday");
            var culture = LocalizationService.Culture;
            var monthFirst = culture.DateTimeFormat.MonthDayPattern.StartsWith('M');
            var pattern = monthFirst ? "MMM d" : "d MMM";
            return modified.ToString(modified.Year == DateTime.Today.Year ? pattern : pattern + " yyyy", culture);
        }
    }
    public override string ToString() => string.Join(", ", new[]
    {
        Title,
        note.Favorite ? Strings.T("NoteStateFavorite") : null,
        note.IsProtected ? Strings.T("NoteStateProtected") : null,
        Description
    }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public string ColorLabel => Strings.T("NoteColor" + note.Color);
    public SolidColorBrush ColorBrush => new(note.Color switch
    {
        NoteColor.Red => ColorHelper.FromArgb(255, 235, 100, 100),
        NoteColor.Orange => ColorHelper.FromArgb(255, 239, 153, 74),
        NoteColor.Yellow => ColorHelper.FromArgb(255, 221, 186, 57),
        NoteColor.Green => ColorHelper.FromArgb(255, 93, 181, 110),
        NoteColor.Teal => ColorHelper.FromArgb(255, 67, 176, 172),
        NoteColor.Blue => ColorHelper.FromArgb(255, 91, 158, 230),
        NoteColor.Purple => ColorHelper.FromArgb(255, 165, 127, 224),
        NoteColor.Pink => ColorHelper.FromArgb(255, 218, 128, 183),
        _ => Colors.Transparent
    });
}

using CommunityToolkit.Mvvm.ComponentModel;
using MarkdownMkII.Core;
using MarkdownMkII.Core.Editor;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Preview;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;

namespace MarkdownMkII.ViewModels;

public partial class NoteEditorViewModel : ObservableObject
{
    public NotePreviewContext? NoteResources { get; set; }
    public string? NoteId { get; private set; }
    public long Version { get; private set; }
    public MarkdownMkII.Storage.NoteSummary? Metadata { get; private set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardTitle))]
    [NotifyPropertyChangedFor(nameof(Header))]
    [NotifyPropertyChangedFor(nameof(FileName))]
    public partial string Title { get; set; } = Strings.T("Untitled");

    internal void ClearSensitiveState()
    {
        metrics = null;
        Text = ""; Parsed = null; DiffAgainstText = null; NoteResources = null;
        savedSnapshot = null; undoHistory.Clear(); historyInitialized = false;
        FindQuery = null; FoldedHeadings.Clear(); Metadata = null; Title = ""; NoteId = null;
        IsDirty = false;
    }
    public void LoadArchiveNote(MarkdownMkII.Storage.NoteDocument note)
    {
        NoteId = note.Summary.Id;
        NoteResources = new(NoteId, new Dictionary<string, EmbeddedNote>());
        Version = note.Version;
        UpdateArchiveMetadata(note.Summary);
        Text = note.Markdown;
        IsDirty = false;
        ResetUndoHistory();
        Reparse();
    }
    public void UpdateArchiveMetadata(MarkdownMkII.Storage.NoteSummary summary)
    {
        Metadata = summary;
        Title = summary.Title;
        OnPropertyChanged(nameof(Metadata));
        OnPropertyChanged(nameof(CardTags));
    }
    internal void CompleteArchiveSave(EditorSaveSnapshot snapshot, long version)
    {
        Version = version;
        savedSnapshot = snapshot;
        UpdateDirtyState();
    }
    private readonly TextUndoHistory undoHistory = new();
    private EditorSaveSnapshot? savedSnapshot;
    private bool historyInitialized;
    private bool restoringHistory;
    private bool typing;
    private (int Start, int Length)? pendingSelection;
    private int caretSelectionLength;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    [NotifyPropertyChangedFor(nameof(CardTitle))]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CardTitle))]
    [NotifyPropertyChangedFor(nameof(CardDescription))]
    [NotifyPropertyChangedFor(nameof(CardTags))]
    [NotifyPropertyChangedFor(nameof(Preview))]
    [NotifyPropertyChangedFor(nameof(Outline))]
    public partial ParsedDocument? Parsed { get; set; }

    [ObservableProperty]
    public partial DocumentStats Stats { get; set; } = WordStats.Compute(string.Empty);

    [ObservableProperty]
    public partial EditorViewMode ViewMode { get; set; } = SettingsService.Instance.Editor.DefaultViewMode;

    [ObservableProperty]
    public partial bool IsLargeFile { get; set; }

    private EditorTextMetrics? metrics;
    public EditorTextMetrics Metrics => metrics is not null && ReferenceEquals(metrics.Text, Text) ? metrics : metrics = new(Text);
    public int CaretStart { get; set; }

    [ObservableProperty]
    public partial string? FindQuery { get; set; }

    [ObservableProperty]
    public partial bool FindBarOpen { get; set; }

    public HashSet<int> FoldedHeadings { get; } = [];

    [ObservableProperty]
    public partial int SlideIndex { get; set; } = -1;

    [ObservableProperty]
    public partial bool KanbanMode { get; set; }

    [ObservableProperty]
    public partial bool DiffMode { get; set; }

    [ObservableProperty]
    public partial bool FocusMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiffMode))]
    public partial string? DiffAgainstText { get; set; }

    partial void OnKanbanModeChanged(bool value)
    {
        if (!value) return;
        DiffMode = false;
        SlideIndex = -1;
    }

    partial void OnSlideIndexChanged(int value)
    {
        if (value < 0) return;
        KanbanMode = false;
        DiffMode = false;
    }

    partial void OnDiffModeChanged(bool value)
    {
        if (value)
        {
            KanbanMode = false;
            SlideIndex = -1;
        }
        else
            DiffAgainstText = null;
    }

    public string FileName => Title;

    public override string ToString() => CardTitle;

    public string Header
    {
        get
        {
            var name = Title;
            return IsDirty ? name + " •" : name;
        }
    }

    public string CardTitle
    {
        get
        {
            if (NoteId is not null) return Title;
            var title = Parsed?.FrontMatter.GetValueOrDefault("title");
            if (string.IsNullOrWhiteSpace(title)) title = Parsed?.Outline.FirstOrDefault()?.Title;
            if (string.IsNullOrWhiteSpace(title)) title = Path.GetFileNameWithoutExtension(FileName);
            return title + (IsDirty ? " •" : "");
        }
    }

    public IReadOnlyList<string> CardTags => Metadata?.Tags.Take(2).ToArray() ?? Parsed?.Tags.Take(2).ToArray() ?? [];

    public string CardDescription
    {
        get
        {
            if (Parsed?.FrontMatter.GetValueOrDefault("description") is { Length: > 0 } description)
                return description;
            // Work from the parsed snapshot, so typing does not scan the document on every key.
            var snapshot = Parsed?.Text ?? string.Empty;
            FrontMatter.TrySplit(snapshot, out _, out var body);
            using var reader = new StringReader(body);
            while (reader.ReadLine() is { } line)
            {
                var clean = line.Trim();
                if (clean.Length == 0 || clean.StartsWith('#') || clean.StartsWith("```") || clean.StartsWith("---")) continue;
                return clean.Length > 160 ? clean[..160] : clean;
            }
            return Strings.T("WorkspaceEmptyNote");
        }
    }

    public PreviewDocument? Preview => Parsed?.Preview;

    public IReadOnlyList<OutlineNode> Outline => Parsed?.Outline ?? [];

    internal EditorSaveSnapshot CaptureSaveSnapshot() => new(Text);

    public bool CanUndo => undoHistory.CanUndo;
    public bool CanRedo => undoHistory.CanRedo;

    public void UpdateSelection(int start, int length)
    {
        CaretStart = Math.Clamp(start, 0, Text.Length);
        caretSelectionLength = Math.Clamp(length, 0, Text.Length - CaretStart);
    }

    public void SetTextFromEditor(string text, int selectionStart, int selectionLength)
    {
        EnsureUndoHistory();
        pendingSelection = (selectionStart, selectionLength);
        typing = true;
        try { Text = text; }
        finally { pendingSelection = null; typing = false; }
        UpdateSelection(selectionStart, selectionLength);
    }

    public void ResetUndoHistory()
    {
        undoHistory.Clear();
        savedSnapshot = IsDirty ? null : CaptureSaveSnapshot();
        historyInitialized = true;
        NotifyUndoHistory();
    }

    internal EditResult? Undo() => RestoreHistory(undo: true);
    internal EditResult? Redo() => RestoreHistory(undo: false);

    private EditResult? RestoreHistory(bool undo)
    {
        var edit = undo ? undoHistory.Undo(Text) : undoHistory.Redo(Text);
        if (edit is { } restored)
        {
            restoringHistory = true;
            try
            {
                Text = restored.Text;
                UpdateSelection(restored.SelectionStart, restored.SelectionLength);
                UpdateDirtyState();
                Reparse(restored.SelectionStart, restored.SelectionLength);
            }
            finally { restoringHistory = false; }
        }

        NotifyUndoHistory();
        return edit;
    }

    private void EnsureUndoHistory()
    {
        if (!historyInitialized) ResetUndoHistory();
    }

    partial void OnTextChanged(string oldValue, string newValue)
    {
        if (!historyInitialized || restoringHistory) return;
        var selection = pendingSelection ?? (Math.Clamp(CaretStart + newValue.Length - oldValue.Length, 0, newValue.Length), 0);
        undoHistory.Record(oldValue, newValue, CaretStart, caretSelectionLength, selection.Item1, selection.Item2, coalesce: typing);
        UpdateSelection(selection.Item1, selection.Item2);
        UpdateDirtyState();
        NotifyUndoHistory();
    }

    private void UpdateDirtyState()
        => IsDirty = savedSnapshot is null || Text != savedSnapshot.Text;

    private void NotifyUndoHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    internal EditResult ApplyEdit(EditResult edit)
    {
        EnsureUndoHistory();
        var start = Math.Clamp(edit.SelectionStart, 0, edit.Text.Length);
        var length = Math.Clamp(edit.SelectionLength, 0, edit.Text.Length - start);
        if (!string.Equals(Text, edit.Text, StringComparison.Ordinal))
        {
            pendingSelection = (start, length);
            try { Text = edit.Text; }
            finally { pendingSelection = null; }
            Reparse(start, length);
        }
        UpdateSelection(start, length);

        return new EditResult(edit.Text, start, length);
    }

    public void Reparse(int selectionStart = 0, int selectionLength = 0)
    {
        EnsureUndoHistory();
        IsLargeFile = DocumentParser.IsLarge(Text);
        Parsed = DocumentParser.Parse(
            Text,
            null,
            selectionStart,
            selectionLength,
            notes: NoteResources);
        Stats = Parsed.Stats;
        HeadingFold.Prune(FoldedHeadings, Outline, Text);
    }

    public void NotifyOutline() => OnPropertyChanged(nameof(Outline));
}

internal sealed record EditorSaveSnapshot(string Text);

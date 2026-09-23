using CommunityToolkit.Mvvm.Input;
using MarkdownMkII.Core.Text;
using MarkdownMkII.Services.Localization;

namespace MarkdownMkII.ViewModels;

public partial class EditorViewModel
{
    [RelayCommand(CanExecute = nameof(HasNote))]
    public void EnsureNoteId()
        => ApplyEdit((text, _, _) =>
        {
            var next = FrontMatter.EnsureId(text);
            return new EditResult(next, 0, 0);
        });

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task EnsureNoteTitleAsync() => RenameAsync();

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task EnsureAliasAsync()
        => PromptFrontMatterAsync("CmdEnsureAlias.Label", Path.GetFileNameWithoutExtension(CurrentNote?.FileName ?? string.Empty), FrontMatter.EnsureAlias);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task EnsureTagsAsync()
        => CurrentNote?.NoteId is { } id ? Services.ArchiveDialogs.TagsAsync(id) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void EnsureDescription()
        => ApplyEdit((text, _, _) => new EditResult(FrontMatter.EnsureDescription(text), 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void EnsureDate()
    {
        var stem = CurrentNote?.Title;
        ApplyEdit((text, _, _) => new EditResult(FrontMatter.EnsureDate(text, DateTime.Now, stem), 0, 0));
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task EnsureStatusAsync()
        => PromptFrontMatterAsync("CmdEnsureStatus.Label", "draft", FrontMatter.EnsureStatus);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task EnsureAuthorAsync()
        => PromptFrontMatterAsync("CmdEnsureAuthor.Label", Environment.UserName, FrontMatter.EnsureAuthor);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task EnsureCssclassAsync()
        => PromptFrontMatterAsync("CmdEnsureCssclass.Label", string.Empty, FrontMatter.EnsureCssclass);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task EnsureLangAsync()
        => PromptFrontMatterAsync("CmdEnsureLang.Label", "it", FrontMatter.EnsureLang);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public Task EnsureTypeAsync()
        => PromptFrontMatterAsync("CmdEnsureType.Label", "note", FrontMatter.EnsureType);

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void StripEmptyYamlKeys()
        => ApplyEdit((text, _, _) => new EditResult(FrontMatter.StripEmptyKeys(text), 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public void SortYamlKeys()
        => ApplyEdit((text, _, _) => new EditResult(FrontMatter.SortKeys(text), 0, 0));

    [RelayCommand(CanExecute = nameof(HasNote))]
    public async Task StripYamlKeyAsync()
    {
        var tab = CurrentNote;
        if (tab is null || host is null)
        {
            return;
        }

        var key = await host.PromptAsync(Strings.T("CmdStripYamlKey.Label"), string.Empty);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        ApplyEdit(tab, (text, _, _) => new EditResult(FrontMatter.RemoveKey(text, key.Trim()), 0, 0));
    }

    [RelayCommand(CanExecute = nameof(HasNote))]
    public async Task RenameYamlKeyAsync()
    {
        var tab = CurrentNote;
        if (tab is null || host is null)
        {
            return;
        }

        var raw = await host.PromptAsync(Strings.T("CmdRenameYamlKey.Label"), "old:new");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var separator = raw.IndexOf(':');
        if (separator <= 0 || separator >= raw.Length - 1)
        {
            return;
        }

        var oldKey = raw[..separator].Trim();
        var newKey = raw[(separator + 1)..].Trim();
        if (oldKey.Length == 0 || newKey.Length == 0)
        {
            return;
        }

        ApplyEdit(tab, (text, _, _) => new EditResult(FrontMatter.RenameKey(text, oldKey, newKey), 0, 0));
    }

    private async Task PromptFrontMatterAsync(
        string titleKey,
        string initialValue,
        Func<string, string, string> transform)
    {
        if (CurrentNote is not { } tab || host is null) return;

        var value = await host.PromptAsync(Strings.T(titleKey), initialValue);
        if (string.IsNullOrWhiteSpace(value) || !ReferenceEquals(tab, CurrentNote)) return;

        ApplyEdit(tab, (text, _, _) => new EditResult(transform(text, value.Trim()), 0, 0));
    }
}

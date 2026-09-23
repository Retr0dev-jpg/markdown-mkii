using Windows.Storage;

namespace MarkdownMkII.ViewModels;

public interface IEditorHost
{
    Task<bool> ConfirmAsync(string title, string message, string primaryButtonText);

    Task<string?> PromptAsync(string title, string? currentValue);

    (int Start, int Length) GetSelection();

    void SetSelection(int start, int length);

    void FocusEditor();

    void ApplyText(string text, int selectionStart, int selectionLength);

    void Print();

    void ShowFind();
    void ShowReplace();
    void RefreshPreviewNow();
    void ToggleInspector();

    void NavigateToEditor();

    Task ShowErrorAsync(string title, string message);

    void SetImmersive(bool immersive);

    void ToggleImmersive();

    void Share();

    void ShowCommandPalette();

    Task<string?> PickAsync(string title, string placeholder, IReadOnlyList<string> items);

    void ShowQuickOpen();

    void ShowGoToHeading();
}

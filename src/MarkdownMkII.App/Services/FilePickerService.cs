using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MarkdownMkII.Services;

public static class FilePickerService
{
    public static async Task<IReadOnlyList<StorageFile>> OpenManyMarkdownAsync()
    {
        var picker = CreateOpenPicker();
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add("*");
        var files = await Paused(picker.PickMultipleFilesAsync);
        return files?.ToList() ?? [];
    }

    public static async Task<StorageFile?> OpenImageAsync()
    {
        var picker = CreateOpenPicker();
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".svg");
        return await Paused(picker.PickSingleFileAsync);
    }

    public static async Task<StorageFile?> SaveMarkdownAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        Initialize(picker);
        picker.SuggestedFileName = string.IsNullOrWhiteSpace(suggestedName) ? "documento.md" : suggestedName;
        picker.FileTypeChoices.Add("Markdown", [".md", ".markdown"]);
        picker.FileTypeChoices.Add("Testo", [".txt"]);
        picker.DefaultFileExtension = ".md";
        return await Paused(picker.PickSaveFileAsync);
    }

    public static async Task<StorageFile?> SaveRecoveryAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        Initialize(picker);
        picker.SuggestedFileName = suggestedName;
        picker.FileTypeChoices.Add(Localization.Strings.T("RecoveryFileType"), [".txt"]);
        picker.DefaultFileExtension = ".txt";
        return await Paused(picker.PickSaveFileAsync);
    }

    public static async Task<StorageFile?> SaveHtmlAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        Initialize(picker);
        picker.SuggestedFileName = Path.ChangeExtension(suggestedName, ".html") ?? "export.html";
        picker.FileTypeChoices.Add("HTML", [".html"]);
        picker.DefaultFileExtension = ".html";
        return await Paused(picker.PickSaveFileAsync);
    }

    public static async Task<StorageFile?> SaveAttachmentAsync(string name)
    {
        var picker = new FileSavePicker();
        Initialize(picker);
        var extension = Path.GetExtension(name);
        if (string.IsNullOrWhiteSpace(extension) || extension.Any(c => !char.IsLetterOrDigit(c) && c != '.')) extension = ".bin";
        picker.SuggestedFileName = Path.GetFileName(name);
        picker.FileTypeChoices.Add(Localization.Strings.T("SecurityAssetFileType"), [extension]);
        return await Paused(picker.PickSaveFileAsync);
    }

    public static async Task<StorageFile?> SaveJsonAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        Initialize(picker);
        picker.SuggestedFileName = Path.ChangeExtension(suggestedName, ".json") ?? "grafo.json";
        picker.FileTypeChoices.Add("JSON", [".json"]);
        picker.DefaultFileExtension = ".json";
        return await Paused(picker.PickSaveFileAsync);
    }

    public static async Task<StorageFolder?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        Initialize(picker);
        picker.FileTypeFilter.Add("*");
        return await Paused(picker.PickSingleFolderAsync);
    }

    public static async Task<StorageFile?> SaveBackupAsync()
    {
        var picker = new FileSavePicker();
        Initialize(picker);
        picker.SuggestedFileName = "Markdown-MkII-" + DateTime.Now.ToString("yyyy-MM-dd");
        picker.FileTypeChoices.Add("Markdown MkII", [".mkii-backup"]);
        return await Paused(picker.PickSaveFileAsync);
    }

    public static async Task<StorageFile?> OpenBackupAsync()
    {
        var picker = CreateOpenPicker();
        picker.FileTypeFilter.Add(".mkii-backup");
        return await Paused(picker.PickSingleFileAsync);
    }

    private static async Task<T> Paused<T>(Func<Windows.Foundation.IAsyncOperation<T>> pick)
    {
        using var pause = NoteSecurity.PauseIdle();
        return await pick();
    }

    private static FileOpenPicker CreateOpenPicker()
    {
        var picker = new FileOpenPicker();
        Initialize(picker);
        return picker;
    }

    private static void Initialize(object picker)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
    }
}

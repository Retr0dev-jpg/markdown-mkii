using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Core.Services;

namespace MarkdownMkII.Services;

public sealed class SettingsService
{
    private const string Stage = "SettingsService";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Lazy<SettingsService> LazyInstance = new(() => new SettingsService());

    public static SettingsService Instance => LazyInstance.Value;

    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly string settingsFilePath;
    private AppSettings settings;

    internal SettingsService(string? filePath = null)
    {
        settingsFilePath = filePath ?? AppPaths.SettingsFile;
        settings = LoadFromDisk(settingsFilePath);
        var migrateWorkspace = settings.Version < 2;
        AppSettingsNormalizer.Normalize(settings);
        if (!settings.IsInitialized || migrateWorkspace)
        {
            settings.IsInitialized = true;
            try
            {
                DocumentStore.WriteAtomic(settingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch (Exception ex)
            {
                DiagnosticsService.LogError(Stage, "Inizializzazione impostazioni fallita", ex);
            }
        }
    }

    public event EventHandler<SettingsChangedEventArgs>? Changed;

    public AppSettings Current => settings;

    public LibrarySettings Library => settings.Library;

    public AppearanceSettings Appearance => settings.Appearance;

    public EditorSettings Editor => settings.Editor;

    public PreviewSettings Preview => settings.Preview;

    public ExportSettings Export => settings.Export;

    public DiagnosticsSettings Diagnostics => settings.Diagnostics;

    public async Task UpdateAsync(SettingsArea area, Action<AppSettings> mutate)
    {
        await MutateAndSaveAsync(mutate);
        Changed?.Invoke(this, new SettingsChangedEventArgs(area));
    }

    public async Task UpdateStateAsync(Action<AppSettings> mutate)
        => await MutateAndSaveAsync(mutate).ConfigureAwait(false);

    public async Task RestorePreferencesAsync(AppSettings restored)
    {
        AppSettingsNormalizer.Normalize(restored);
        await writeGate.WaitAsync();
        try { settings = restored; await WriteUnlockedAsync(throwOnError: true); }
        finally { writeGate.Release(); }
        Changed?.Invoke(this, new SettingsChangedEventArgs(SettingsArea.All));
    }

    public async Task ResetAsync()
    {
        await writeGate.WaitAsync();
        try
        {
            var language = settings.Appearance.Language;
            settings = new AppSettings { IsInitialized = true, Version = 2 };
            settings.Appearance.Language = language;
            await WriteUnlockedAsync();
        }
        finally
        {
            writeGate.Release();
        }

        Changed?.Invoke(this, new SettingsChangedEventArgs(SettingsArea.All));
    }

    public static string FilePath => Instance.settingsFilePath;

    private static AppSettings LoadFromDisk(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, "Caricamento delle impostazioni fallito", ex);
            return new AppSettings();
        }
    }

    private async Task MutateAndSaveAsync(Action<AppSettings> mutate)
    {
        await writeGate.WaitAsync();
        try
        {
            mutate(settings);
            AppSettingsNormalizer.Normalize(settings);
            await WriteUnlockedAsync().ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }

    public async Task FlushAsync()
    {
        // Every mutation includes its disk write. Acquiring the gate waits for earlier writes.
        await writeGate.WaitAsync().ConfigureAwait(false);
        writeGate.Release();
    }

    private async Task WriteUnlockedAsync(bool throwOnError = false)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await Task.Run(() => DocumentStore.WriteAtomic(settingsFilePath, json)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError(Stage, "Salvataggio delle impostazioni fallito", ex);
            if (throwOnError) throw;
        }
    }
}

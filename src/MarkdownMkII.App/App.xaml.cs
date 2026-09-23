using MarkdownMkII.Core.Services;
using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.Globalization;

namespace MarkdownMkII;

public partial class App : Application
{
    private MainWindow? window;

    public static Window? MainWindow { get; private set; }

    public App()
    {
        Services.Interop.ProcessHardening.ExcludeHeapFromCrashReports();
        ApplyLanguageOverride();
        InitializeComponent();
        LocalizationService.Initialize();

        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void ApplyLanguageOverride()
    {
        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = LocalizationService.EffectiveLanguage(
                SettingsService.Instance.Appearance.Language, Windows.System.UserProfile.GlobalizationPreferences.Languages);
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("App", "Lingua dell'interfaccia non applicata", ex);
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var current = AppInstance.GetCurrent();
        var main = AppInstance.FindOrRegisterForKey("MarkdownMkII");
        if (!main.IsCurrent)
        {
            try
            {
                await main.RedirectActivationToAsync(current.GetActivatedEventArgs());
            }
            catch (Exception ex)
            {
                DiagnosticsService.LogError("App", "Redirect attivazione non riuscito", ex);
            }

            Environment.Exit(0);
            return;
        }

        main.Activated += OnRedirectedActivation;
        window = new MainWindow();
        MainWindow = window;
        window.Activate();
        window.ShowEmptyWorkspace();
        _ = HandleActivationAsync(current.GetActivatedEventArgs());
        _ = AppUpdates.CheckAtStartupAsync();
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments e)
        => window?.DispatcherQueue.TryEnqueue(() => _ = HandleActivationAsync(e));

    private static async Task HandleActivationAsync(AppActivationArguments activated)
    {
        try
        {
            if (activated.Kind != ExtendedActivationKind.File ||
                activated.Data is not Windows.ApplicationModel.Activation.IFileActivatedEventArgs fileArgs ||
                App.MainWindow is not MainWindow main)
            {
                App.MainWindow?.Activate();
                return;
            }

            await ArchiveDialogs.ImportAsync(fileArgs.Files.OfType<Windows.Storage.StorageFile>().Select(f => f.Path));
            main.Activate();
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("App", "Apertura file da istanza esistente non riuscita", ex);
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        DiagnosticsService.LogError("App.UnhandledException", e.Message, e.Exception);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        DiagnosticsService.LogError(
            "AppDomain.UnhandledException",
            e.IsTerminating ? "Eccezione fatale sul thread in background" : "Eccezione sul thread in background",
            e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        DiagnosticsService.LogError("TaskScheduler.UnobservedTaskException", "Task non osservato", e.Exception);
        e.SetObserved();
    }
}

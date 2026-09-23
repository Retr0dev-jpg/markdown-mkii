using MarkdownMkII.Services;
using MarkdownMkII.Services.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownMkII.Views;

public sealed partial class SecuritySettings : UserControl
{
    private bool loading = true;
    private bool working;
    private bool ready;
    private int refreshGeneration;
    private bool CanRefresh => ready && App.MainWindow is MainWindow { IsClosed: false };
    private static readonly int[] Minutes = [1, 5, 15, 30];
    public SecuritySettings()
    {
        InitializeComponent();
        foreach (var minutes in Minutes) IdleBox.Items.Add(Strings.Format(Strings.DefaultMap, "SecurityMinutes", minutes));
        Loaded += async (_, _) => { ready = true; NoteSecurity.Changed += OnChanged; await RefreshAsync(); };
        Unloaded += (_, _) => { ready = false; refreshGeneration++; NoteSecurity.Changed -= OnChanged; };
    }
    private async void OnChanged(object? sender, EventArgs e) { if (!working) await RefreshAsync(); }
    private async Task RefreshAsync()
    {
        if (!CanRefresh) return;
        var generation = ++refreshGeneration;
        loading = true;
        try
        {
            var settings = await NoteArchive.Database.ProtectionSettingsAsync();
            var enabled = await WindowsHelloProtection.IsEnabledAsync();
            var available = enabled || await WindowsHelloProtection.AvailableAsync();
            if (!CanRefresh || generation != refreshGeneration) return;
            ConfigureButton.Visibility = settings.Configured ? Visibility.Collapsed : Visibility.Visible;
            ChangePasswordButton.Visibility = settings.Configured ? Visibility.Visible : Visibility.Collapsed;
            RecoverButton.IsEnabled = RenewButton.IsEnabled = settings.Configured;
            HideDetailsSwitch.IsEnabled = IdleBox.IsEnabled = settings.Configured;
            HideDetailsSwitch.IsOn = settings.HideDetails;
            IdleBox.SelectedIndex = Array.IndexOf(Minutes, settings.IdleMinutes);
            HelloButton.Content = Strings.T(enabled ? "SecurityDisableHello" : "SecurityEnableHello");
            HelloButton.IsEnabled = settings.Configured && available;
            UnlockButton.IsEnabled = settings.Configured && !NoteArchive.Database.IsUnlocked;
            LockButton.IsEnabled = NoteArchive.Database.IsUnlocked;
            StateMessage.Message = Strings.T(!settings.Configured ? "SecurityNotConfigured" : NoteArchive.Database.IsUnlocked ? "SecuritySessionOpen" : "SecuritySessionLocked");
            RetryMaintenanceButton.Visibility = settings.MaintenancePending ? Visibility.Visible : Visibility.Collapsed;
            StateMessage.Severity = settings.MaintenancePending ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
            if (settings.MaintenancePending) StateMessage.Message = Strings.T("SecurityMaintenancePending");
            if (App.MainWindow is MainWindow { Editor.HasProtectedDraft: true }) StateMessage.Message = Strings.T("SecurityPendingSave");
            await NoteSecurity.RefreshSettingsAsync();
        }
        catch { if (CanRefresh && generation == refreshGeneration) StateMessage.Message = Strings.T("SecurityActionFailed"); }
        finally { if (generation == refreshGeneration) loading = false; }
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (working) return;
        working = true;
        try { await action(); }
        catch { if (CanRefresh) StateMessage.Message = Strings.T("SecurityActionFailed"); }
        finally { working = false; await RefreshAsync(); }
    }
    private async void OnRetryMaintenance(object sender, RoutedEventArgs e) => await RunAsync(NoteSecurity.RetryMaintenanceAsync);
    private async void OnConfigure(object sender, RoutedEventArgs e) => await RunAsync(async () => { await NoteSecurity.ConfigureAsync(); });
    private async void OnPassword(object sender, RoutedEventArgs e) => await RunAsync(NoteSecurity.ChangePasswordAsync);
    private async void OnRenew(object sender, RoutedEventArgs e) => await RunAsync(NoteSecurity.RenewRecoveryAsync);
    private async void OnRecover(object sender, RoutedEventArgs e) => await RunAsync(NoteSecurity.RecoverAsync);
    private async void OnHello(object sender, RoutedEventArgs e) => await RunAsync(NoteSecurity.ToggleHelloAsync);
    private async void OnUnlock(object sender, RoutedEventArgs e) => await RunAsync(async () => { await NoteSecurity.EnsureUnlockedAsync(); });
    private async void OnLock(object sender, RoutedEventArgs e) => await RunAsync(NoteSecurity.LockAsync);
    private async void OnVisibility(object sender, RoutedEventArgs e)
    {
        if (!loading && !working) await RunAsync(() => NoteSecurity.ApplySettingsAsync(HideDetailsSwitch.IsOn, Minutes[Math.Max(0, IdleBox.SelectedIndex)]));
    }
    private async void OnIdle(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && !working && IdleBox.SelectedIndex >= 0) await RunAsync(() => NoteSecurity.ApplySettingsAsync(HideDetailsSwitch.IsOn, Minutes[IdleBox.SelectedIndex]));
    }
}

using CommunityToolkit.Mvvm.Input;
using MarkdownMkII.Core.Editor;
using MarkdownMkII.Core.Models;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.ViewModels;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace MarkdownMkII.Services;
public static class ShortcutService
{
    public static bool Capturing { get; set; }

    public static string? Effective(PaletteCommand command) => SettingsService.Instance.Current.Shortcuts.TryGetValue(command.Id, out var value) ? value : command.DefaultShortcut;
    public static CommandGesture Current(VirtualKey key)
    {
        bool Down(VirtualKey k) => InputKeyboardSource.GetKeyStateForCurrentThread(k).HasFlag(CoreVirtualKeyStates.Down);
        if (Down(VirtualKey.RightMenu) || Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows))
            return default;
        var modifiers = (Down(VirtualKey.Control) ? GestureModifiers.Control : 0) | (Down(VirtualKey.Shift) ? GestureModifiers.Shift : 0) | (Down(VirtualKey.Menu) ? GestureModifiers.Alt : 0);
        return new((int)key, modifiers);
    }

    public static string? Conflict(PaletteCommand command, CommandGesture gesture, IEnumerable<PaletteCommand> commands) => commands.FirstOrDefault(c => c.Id != command.Id && CommandGesture.TryParse(c.Shortcut, out var other) && Interop.KeyboardText.ForCurrentLayout(other) == Interop.KeyboardText.ForCurrentLayout(gesture))?.Label;
    public static Task AssignAsync(string id, string? gesture)
    {
        if (gesture is not null)
        {
            var commands = EditorPalette.Commands(((MainWindow)App.MainWindow!).Editor);
            var command = commands.Single(c => c.Id == id);
            if (!CommandGesture.TryParse(gesture, out var parsed) || !parsed.CanAssign)
                throw new ArgumentException(Strings.T("ShortcutReserved"));
            if (Conflict(command, parsed, commands)is { } other)
                throw new InvalidOperationException(Strings.Format(Strings.DefaultMap, "ShortcutConflict", other));
        }

        return SettingsService.Instance.UpdateAsync(SettingsArea.Commands, s => s.Shortcuts[id] = gesture);
    }

    public static Task ResetAsync(string? id = null) => SettingsService.Instance.UpdateAsync(SettingsArea.Commands, s =>
    {
        if (id is null)
            s.Shortcuts.Clear();
        else
            s.Shortcuts.Remove(id);
    });
    public static async Task ExecuteAsync(PaletteCommand command)
    {
        if (!command.Command.CanExecute(null))
            return;
        try
        {
            if (command.Command is IAsyncRelayCommand asyncCommand)
                await asyncCommand.ExecuteAsync(null);
            else
                command.Command.Execute(null);
            await SettingsService.Instance.UpdateStateAsync(s => s.RecentCommands = new[] { command.Id }.Concat(s.RecentCommands).Distinct().Take(20).ToList());
        }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Command", command.Id, ex);
            await ArchiveDialogs.MessageAsync(Strings.T("ArchiveActionFailed"), ex.Message);
        }
    }

    public static bool TryExecute(VirtualKey key, EditorViewModel vm, bool editorFocus)
    {
        if (Capturing || NoteSecurity.Blocking)
            return false;
        if (App.MainWindow?.Content.XamlRoot is { } root && Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Any(p => ContainsDialog(p.Child)))
            return false;
        var gesture = Current(key);
        if (gesture.Key == 0)
            return false;
        var command = EditorPalette.Commands(vm).FirstOrDefault(c => (editorFocus || c.IsGlobal) && CommandGesture.TryParse(c.Shortcut, out var bound) && Interop.KeyboardText.ForCurrentLayout(bound) == gesture);
        if (command is null || !command.Command.CanExecute(null) || (!command.IsGlobal && vm.CurrentNote is null))
            return false;
        _ = ExecuteAsync(command);
        return true;
    }

    private static bool ContainsDialog(Microsoft.UI.Xaml.DependencyObject? element)
    {
        if (element is null)
            return false;
        if (element is Microsoft.UI.Xaml.Controls.ContentDialog)
            return true;
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            if (ContainsDialog(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(element, i)))
                return true;
        return false;
    }
}

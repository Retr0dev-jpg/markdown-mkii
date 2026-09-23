using MarkdownMkII.Core.Models;

namespace MarkdownMkII.Services;

public sealed class SettingsChangedEventArgs : EventArgs
{
    public SettingsChangedEventArgs(SettingsArea areas)
    {
        Areas = areas;
    }

    public SettingsArea Areas { get; }

    public bool Includes(SettingsArea area) => (Areas & area) != SettingsArea.None;
}

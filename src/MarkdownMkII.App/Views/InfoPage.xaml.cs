using MarkdownMkII.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace MarkdownMkII.Views;

public sealed partial class InfoPage : Page
{
    public InfoViewModel ViewModel { get; } = new();

    public InfoPage()
    {
        InitializeComponent();
    }

    private void OnCopyPath(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.FrameworkElement { Tag: string path }) return;
        var data = new Windows.ApplicationModel.DataTransfer.DataPackage(); data.SetText(path);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
    }
    private async void OnOpenPath(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement { Tag: string path })
            await Windows.System.Launcher.LaunchFolderPathAsync(System.IO.Path.GetDirectoryName(path));
    }

}

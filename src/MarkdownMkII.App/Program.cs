using Velopack;

namespace MarkdownMkII;

public static class Program
{
    // Velopack must run first: during install, update and uninstall it handles the call and exits.
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build().Run();
        XamlGeneratedProgram.XamlGeneratedMain();
    }
}

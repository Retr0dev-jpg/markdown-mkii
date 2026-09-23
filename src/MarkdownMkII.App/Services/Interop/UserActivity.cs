using System.Runtime.InteropServices;

namespace MarkdownMkII.Services.Interop;

/// <summary>
/// Detects input that WinUI routing does not deliver to the main tree, such as typing inside a
/// ContentDialog, while the app window is in the foreground.
/// </summary>
internal static class UserActivity
{
    public static bool HadRecentInput(nint window, TimeSpan within)
    {
        if (GetForegroundWindow() != window) return false;
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return false;
        var elapsed = unchecked((uint)Environment.TickCount - info.Time);
        return elapsed <= within.TotalMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint GetForegroundWindow();
}

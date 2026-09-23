using System.Runtime.InteropServices;

namespace MarkdownMkII.Services.Interop;

internal static class WindowPlacementInterop
{
    private const uint MonitorDefaultToNull = 0;

    public static bool IntersectsAnyMonitor(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var right = (long)x + width;
        var bottom = (long)y + height;
        if (right > int.MaxValue || bottom > int.MaxValue)
        {
            return false;
        }

        var rectangle = new NativeRect { Left = x, Top = y, Right = (int)right, Bottom = (int)bottom };
        return MonitorFromRect(ref rectangle, MonitorDefaultToNull) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint MonitorFromRect(ref NativeRect rectangle, uint flags);
}

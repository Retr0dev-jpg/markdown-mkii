using System.Runtime.InteropServices;

namespace MarkdownMkII.Services.Interop;

internal sealed class SecuritySessionMonitor : IDisposable
{
    private readonly nint window;
    private readonly Action lockSession;
    private readonly SubclassProc callback;
    private const nuint SubclassId = 0x4D4B5345;
    internal SecuritySessionMonitor(nint window, Action lockSession)
    {
        this.window = window;
        this.lockSession = lockSession;
        callback = WindowProc;
        if (!SetWindowSubclass(window, callback, SubclassId, 0)) throw new InvalidOperationException("Unable to observe session security.");
        if (!WTSRegisterSessionNotification(window, 0))
        {
            RemoveWindowSubclass(window, callback, SubclassId);
            throw new InvalidOperationException("Unable to observe Windows session locking.");
        }
    }
    private nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == 0x02B1 && wParam is 7 or 6 || message == 0x0218 && wParam == 4)
            lockSession();
        return DefSubclassProc(hwnd, message, wParam, lParam);
    }
    public void Dispose()
    {
        WTSUnRegisterSessionNotification(window);
        RemoveWindowSubclass(window, callback, SubclassId);
        GC.KeepAlive(callback);
    }
    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
    [DllImport("wtsapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSRegisterSessionNotification(nint hwnd, uint flags);
    [DllImport("wtsapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSUnRegisterSessionNotification(nint hwnd);
}

using System.Runtime;
using System.Runtime.InteropServices;

namespace MarkdownMkII.Services.Interop;

/// <summary>Limits where decrypted note text can outlive its use inside this process.</summary>
internal static class ProcessHardening
{
    private const uint WerNoHeap = 0x1;

    /// <summary>Crash reports keep stacks and modules but no heap memory, where decrypted text lives.</summary>
    public static void ExcludeHeapFromCrashReports()
    {
        try { _ = WerSetFlags(WerNoHeap); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    /// <summary>
    /// .NET strings cannot be wiped, so after a lock the released text is compacted away, including the
    /// large object heap, and the freed pages are returned to Windows, which zeroes them before reuse.
    /// </summary>
    public static void ReleaseFreedMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int WerSetFlags(uint flags);
}

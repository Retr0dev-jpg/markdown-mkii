using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MarkdownMkII.Storage;

/// <summary>
/// Buffers for key material. They live on the pinned heap, so the GC never leaves a stale copy behind
/// that zeroing could miss; keys cached for a session are also locked out of the page file when possible.
/// </summary>
internal static class SecretMemory
{
    public static byte[] Pinned(int length) => GC.AllocateArray<byte>(length, pinned: true);

    public static byte[] Random(int length)
    {
        var result = Pinned(length);
        RandomNumberGenerator.Fill(result);
        return result;
    }

    public static byte[] Copy(ReadOnlySpan<byte> value, bool resident = false)
    {
        var result = Pinned(value.Length);
        value.CopyTo(result);
        if (resident) Lock(result);
        return result;
    }

    /// <summary>Moves a key into a resident buffer and wipes the source.</summary>
    public static byte[] Adopt(byte[] value)
    {
        try { return Copy(value, resident: true); }
        finally { CryptographicOperations.ZeroMemory(value); }
    }

    // Best effort: the pages stay locked for the life of the process because other keys may share them.
    private static void Lock(byte[] buffer)
    {
        if (!OperatingSystem.IsWindows() || buffer.Length == 0) return;
        _ = VirtualLock(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0), (nuint)buffer.Length);
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualLock(nint address, nuint size);
}

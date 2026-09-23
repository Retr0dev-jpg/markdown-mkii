using System.Runtime.InteropServices;
using System.Text;
using MarkdownMkII.Storage;
using Microsoft.Win32;

namespace MarkdownMkII.Services.Interop;

/// <summary>
/// Keeps each archive's integrity anchor in the user's registry, apart from the folder that holds the
/// archive, and protects it with DPAPI so it is bound to this Windows account.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal sealed class IntegrityAnchorStore : IIntegrityAnchor
{
    private const string KeyPath = @"Software\Markdown MkII\Integrity";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MarkdownMkII.IntegrityAnchor.v1");

    public byte[]? Load(string archiveId)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(archiveId) is byte[] value ? Unprotect(value) : null;
    }

    public void Store(string archiveId, byte[] value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(archiveId, Protect(value), RegistryValueKind.Binary);
    }

    public void Forget(string archiveId)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(archiveId, throwOnMissingValue: false);
    }

    /// <summary>Removes the anchors of every archive this account ever sealed; used by the factory reset.</summary>
    public static void ForgetAll() => Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);

    private static byte[] Protect(byte[] value) => Transform(value, protect: true);
    private static byte[]? Unprotect(byte[] value)
    {
        try { return Transform(value, protect: false); }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    private static byte[] Transform(byte[] value, bool protect)
    {
        var input = GCHandle.Alloc(value, GCHandleType.Pinned);
        var entropy = GCHandle.Alloc(Entropy, GCHandleType.Pinned);
        try
        {
            var inBlob = new DataBlob { Size = value.Length, Data = input.AddrOfPinnedObject() };
            var entropyBlob = new DataBlob { Size = Entropy.Length, Data = entropy.AddrOfPinnedObject() };
            DataBlob output;
            var ok = protect
                ? CryptProtectData(ref inBlob, null, ref entropyBlob, 0, 0, UiForbidden, out output)
                : CryptUnprotectData(ref inBlob, 0, ref entropyBlob, 0, 0, UiForbidden, out output);
            if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, output.Size);
                return result;
            }
            finally { LocalFree(output.Data); }
        }
        finally
        {
            input.Free();
            entropy.Free();
        }
    }

    private const int UiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public nint Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string? description, ref DataBlob entropy, nint reserved, nint prompt, int flags, out DataBlob output);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob input, nint description, ref DataBlob entropy, nint reserved, nint prompt, int flags, out DataBlob output);

    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint memory);
}

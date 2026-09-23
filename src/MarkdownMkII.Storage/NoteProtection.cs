using System.Security.Cryptography;
using System.Text;

namespace MarkdownMkII.Storage;

public sealed class NoteLockedException() : InvalidOperationException("The protected archive is locked.");
public sealed class ProtectionAuthenticationException() : CryptographicException("Unable to unlock the protected archive.");
public sealed record ProtectionSettings(bool Configured, bool HideDetails, int IdleMinutes, bool MaintenancePending = false);
public sealed record SealedNoteDraft(string NoteId, long Version, byte[] Data);

/// <summary>Protected content that no longer matches the archive seal, found while unlocking.</summary>
/// <param name="RolledBack">The whole archive is older than the last state this device sealed.</param>
/// <param name="Unverifiable">The seal itself is missing or altered, so no single note can be trusted or blamed.</param>
/// <param name="NoteIds">Notes whose encrypted records were replaced, added or removed outside the app.</param>
public sealed record ArchiveIntegrityReport(bool RolledBack, bool Unverifiable, IReadOnlyList<string> NoteIds);
public sealed class ArchiveIntegrityException() : InvalidOperationException("The protected archive failed its integrity check.");

/// <summary>
/// Keeps the latest sealed generation of each archive outside the database file, so replacing the
/// whole file with an older copy is detected. Values are opaque and authenticated by the archive.
/// </summary>
public interface IIntegrityAnchor
{
    byte[]? Load(string archiveId);
    void Store(string archiveId, byte[] value);
    void Forget(string archiveId);
}

public interface INoteProtection
{
    bool IsUnlocked { get; }
    long SessionVersion { get; }
    event EventHandler? ProtectionChanged;
    Task<ProtectionSettings> ProtectionSettingsAsync();
    Task<string> ConfigureProtectionAsync(string password);
    Task UnlockAsync(string password);
    Task<bool> TryUnlockAsync(string password);
    Task<bool> TryChangePasswordAsync(string current, string replacement);
    Task<PreparedCredentials?> PrepareCredentialsAsync(CredentialChange change, string credential, string? newPassword = null);
    Task CommitCredentialsAsync(PreparedCredentials prepared);
    Task CancelCredentialsAsync(PreparedCredentials prepared);
    Task LockAsync();
    Task ChangePasswordAsync(string oldPassword, string newPassword);
    Task<string> RecoverAsync(string recoveryCode, string newPassword);
    Task<string> RenewRecoveryCodeAsync(string password);
    Task SetProtectionSettingsAsync(bool hideDetails, int idleMinutes);
    Task ProtectAsync(string noteId);
    Task UnprotectAsync(string noteId);
}

/// <summary>Shared by the UI and the archive: at least 12 characters, at least 5 of them different.</summary>
public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MinimumDistinct = 5;

    public static bool IsAcceptable(string? password)
    {
        var runes = (password ?? string.Empty).EnumerateRunes().ToList();
        return runes.Count >= MinimumLength && runes.Distinct().Count() >= MinimumDistinct;
    }
}

internal static class NoteCipher
{
    internal const int Iterations = 600_000;
    internal static byte[] PasswordKey(string password, byte[] salt, int iterations)
    {
        if (salt.Length != 32 || iterations is < Iterations or > 5_000_000)
            throw new CryptographicException("Invalid password key parameters.");
        var result = SecretMemory.Pinned(32);
        Rfc2898DeriveBytes.Pbkdf2(password, salt, result, iterations, HashAlgorithmName.SHA256);
        return result;
    }

    internal static void ValidatePassword(string password)
    {
        if (!PasswordPolicy.IsAcceptable(password))
            throw new ArgumentException("A password must contain at least 12 characters, of which at least 5 different.");
    }

    // Format 1: version, 96-bit nonce, 128-bit authentication tag, ciphertext.
    internal static byte[] Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, string context)
    {
        var result = new byte[29 + value.Length];
        result[0] = 1;
        RandomNumberGenerator.Fill(result.AsSpan(1, 12));
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(result.AsSpan(1, 12), value, result.AsSpan(29), result.AsSpan(13, 16), Encoding.UTF8.GetBytes(context));
        return result;
    }

    private const int PaddingBlock = 256;
    private const string PaddedContext = ":padded";

    // Format 2: as format 1, but the plaintext is a 4-byte length, the value and zero padding up to a
    // multiple of 256 bytes, so the stored size no longer reveals the exact length of notes and files.
    internal static byte[] EncryptPadded(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, string context)
    {
        var padded = new byte[(value.Length + 4 + PaddingBlock - 1) / PaddingBlock * PaddingBlock];
        try
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(padded, value.Length);
            value.CopyTo(padded.AsSpan(4));
            // The format byte itself is not authenticated, so format 2 also changes the associated data.
            var result = Encrypt(key, padded, context + PaddedContext);
            result[0] = 2;
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(padded); }
    }

    internal static byte[] Decrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, string context)
    {
        if (value.Length < 29 || value[0] is not (1 or 2))
            throw new CryptographicException("Invalid encrypted record.");
        var result = Buffer(value.Length - 29);
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(value.Slice(1, 12), value[29..], value.Slice(13, 16), result, Encoding.UTF8.GetBytes(value[0] == 2 ? context + PaddedContext : context));
            if (value[0] == 1) return result;
            var length = result.Length >= 4 ? System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(result) : -1;
            if (length < 0 || length > result.Length - 4) throw new CryptographicException("Invalid encrypted record.");
            var unpadded = Buffer(length);
            result.AsSpan(4, length).CopyTo(unpadded);
            CryptographicOperations.ZeroMemory(result);
            return unpadded;
        }
        catch { CryptographicOperations.ZeroMemory(result); throw; }
    }

    // Keys are small; large bodies and attachments stay on the normal heap to avoid pinned fragmentation.
    private static byte[] Buffer(int length) => length <= 64 ? SecretMemory.Pinned(length) : new byte[length];

    internal static byte[] EncryptText(byte[] key, string value, string context)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        try { return EncryptPadded(key, bytes, context); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static string DecryptText(byte[] key, byte[] value, string context)
    {
        var bytes = Decrypt(key, value, context);
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static string RecoveryCode(byte[] bytes) => string.Join("-", Convert.ToHexString(bytes).Chunk(8).Select(c => new string(c)));
    internal static byte[] RecoveryKey(string code)
    {
        var compact = code.Replace("-", "").Replace(" ", "").Trim();
        var bytes = SecretMemory.Pinned(32);
        if (compact.Length == 64 && Convert.FromHexString(compact, bytes, out _, out var written) == System.Buffers.OperationStatus.Done && written == 32)
            return bytes;
        CryptographicOperations.ZeroMemory(bytes);
        throw new ProtectionAuthenticationException();
    }
}

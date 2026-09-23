using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Windows.Security.Credentials.UI;

namespace MarkdownMkII.Services;

/// <summary>Device-only key wrapping. The consent dialog alone is never an unlock credential.</summary>
internal static class WindowsHelloProtection
{
    private static readonly CngProvider Provider = new("Microsoft Passport Key Storage Provider");
    private static string FilePath => Path.Combine(AppPaths.Root, "windows-hello.json");
    private sealed record DeviceCredential(string ArchiveId, string KeyName, byte[] WrappedKey);

    private static Task<bool>? availability;
    private static DateTimeOffset availabilityTime;
    internal static Task<bool> AvailableAsync()
    {
        if (availability is null || DateTimeOffset.UtcNow - availabilityTime > TimeSpan.FromMinutes(1))
        {
            availabilityTime = DateTimeOffset.UtcNow;
            availability = CheckAvailabilityAsync();
        }
        return availability;
    }
    private static async Task<bool> CheckAvailabilityAsync()
    {
        try { return await UserConsentVerifier.CheckAvailabilityAsync() == UserConsentVerifierAvailability.Available; }
        catch { return false; }
    }

    internal static async Task<bool> IsEnabledAsync()
    {
        var archive = await NoteArchive.Database.ArchiveIdentityAsync();
        return await Task.Run(() =>
        {
            var credential = Read();
            return credential?.ArchiveId == archive && CngKey.Exists(credential.KeyName, Provider);
        });
    }

    internal static async Task<bool> EnableAsync(string password, nint window)
    {
        var archive = await NoteArchive.Database.ArchiveIdentityAsync();
        var name = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value + "//MarkdownMkII/" + archive + "/" + Guid.NewGuid().ToString("N");
        try
        {
            var wrapped = await NoteArchive.Database.TryWrapForDeviceAsync(password, bytes =>
            {
                var parameters = new CngKeyCreationParameters
                {
                    Provider = Provider, ExportPolicy = CngExportPolicies.None,
                    KeyUsage = CngKeyUsages.Decryption, ParentWindowHandle = window
                };
                parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(2048), CngPropertyOptions.None));
                parameters.Parameters.Add(new CngProperty("NgcCacheType", BitConverter.GetBytes(1), CngPropertyOptions.None));
                using var key = CngKey.Create(CngAlgorithm.Rsa, name, parameters);
                RequireProtectedKey(key, window);
                using var rsa = new RSACng(key);
                var cipher = rsa.Encrypt(bytes, RSAEncryptionPadding.OaepSHA256);
                // Enrollment must establish that this exact provider supports OAEP and user verification.
                var unwrapped = rsa.Decrypt(cipher, RSAEncryptionPadding.OaepSHA256);
                try
                {
                    if (!CryptographicOperations.FixedTimeEquals(bytes, unwrapped)) throw new CryptographicException();
                }
                finally { CryptographicOperations.ZeroMemory(unwrapped); }
                return cipher;
            });
            if (wrapped is null) return false;
            await Task.Run(() =>
            {
                var previous = Read();
                var temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(new DeviceCredential(archive, name, wrapped)));
                File.Move(temporary, FilePath, true);
                DeleteKey(previous?.KeyName);
            });
            return true;
        }
        catch { await Task.Run(() => DeleteKey(name)); throw; }
    }

    internal static async Task UnlockAsync(nint window)
    {
        var archive = await NoteArchive.Database.ArchiveIdentityAsync();
        await NoteArchive.Database.UnlockWithDeviceAsync(() =>
        {
            var credential = Read();
            if (credential is null || credential.ArchiveId != archive) throw new CryptographicException();
            using var key = CngKey.Open(credential.KeyName, Provider);
            RequireProtectedKey(key, window);
            using var rsa = new RSACng(key);
            return rsa.Decrypt(credential.WrappedKey, RSAEncryptionPadding.OaepSHA256);
        });
    }

    private static void RequireProtectedKey(CngKey key, nint window)
    {
        var policy = key.GetProperty("NgcCacheType", CngPropertyOptions.None).GetValue();
        if (policy is null || policy.Length < 4 || !Equals(key.Provider, Provider) || key.ExportPolicy != CngExportPolicies.None || !Equals(key.Algorithm, CngAlgorithm.Rsa) || key.KeyUsage != CngKeyUsages.Decryption ||
            BitConverter.ToInt32(policy) != 1)
            throw new CryptographicException("Windows Hello key policy is unavailable.");
        key.ParentWindowHandle = window;
        key.SetProperty(new("PinCacheIsGestureRequired", BitConverter.GetBytes(1), CngPropertyOptions.None));
        key.SetProperty(new("Use Context", Encoding.Unicode.GetBytes("Markdown MkII\0"), CngPropertyOptions.None));
    }

    internal static Task DisableAsync() => Task.Run(() =>
    {
        var credential = Read();
        if (File.Exists(FilePath)) File.Delete(FilePath);
        DeleteKey(credential?.KeyName);
    });

    private static DeviceCredential? Read()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<DeviceCredential>(File.ReadAllText(FilePath)) : null; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private static void DeleteKey(string? name)
    {
        if (name is null) return;
        try
        {
            if (!CngKey.Exists(name, Provider)) return;
            using var key = CngKey.Open(name, Provider);
            key.Delete();
        }
        catch (CryptographicException) { /* Never remove or fall back to a less protected provider. */ }
    }
}

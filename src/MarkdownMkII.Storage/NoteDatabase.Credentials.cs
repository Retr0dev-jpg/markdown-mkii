using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage;

public enum CredentialChange { Configure, Recover, RenewRecovery }

/// <summary>
/// Credentials prepared before the user saves the recovery file. Raw secrets stay in memory only and
/// are zeroed on dispose; the commit rotates the master key and wraps it with them.
/// </summary>
public sealed class PreparedCredentials : IDisposable
{
    public string RecoveryCode { get; private set; }
    public string ArchiveId { get; }
    public DateTimeOffset Created { get; } = DateTimeOffset.UtcNow;
    internal byte[] MasterKey { get; }
    internal byte[] Salt { get; }
    internal byte[] DerivedPasswordKey { get; }
    internal byte[] RecoverySecret { get; }
    internal byte[]? PreviousPasswordKey { get; }
    internal byte[]? PreviousRecoveryKey { get; }
    internal long Session { get; }
    internal bool Disposed { get; private set; }
    internal PreparedCredentials(string code, string archive, long session, byte[] key, byte[] salt,
        byte[] derivedPasswordKey, byte[] recoverySecret, byte[]? previousPassword, byte[]? previousRecovery)
    {
        RecoveryCode = code; ArchiveId = archive; Session = session; MasterKey = key;
        Salt = salt; DerivedPasswordKey = derivedPasswordKey; RecoverySecret = recoverySecret;
        PreviousPasswordKey = previousPassword; PreviousRecoveryKey = previousRecovery;
    }
    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true; RecoveryCode = "";
        foreach (var bytes in new[] { MasterKey, Salt, DerivedPasswordKey, RecoverySecret, PreviousPasswordKey, PreviousRecoveryKey })
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
    }
}

public sealed partial class NoteDatabase
{
    private readonly List<PreparedCredentials> pendingCredentials = [];

    private byte[]? TryAuthenticate(SqliteConnection db, string password)
    {
        using var cmd = Command(db, "SELECT Salt,Iterations,PasswordKey FROM Protection WHERE Id=1");
        using var r = cmd.ExecuteReader();
        if (!r.Read() || r.IsDBNull(0)) return null;
        var derived = NoteCipher.PasswordKey(password, (byte[])r[0], r.GetInt32(1));
        try { return TryUnwrap(derived, (byte[])r[2], Context("vault", "password")); }
        finally { CryptographicOperations.ZeroMemory(derived); }
    }
    private static byte[]? TryUnwrap(byte[] key, byte[] value, string context)
    {
        try { return NoteCipher.Decrypt(key, value, context); }
        catch (AuthenticationTagMismatchException) { return null; }
    }
    public async Task<bool> TryUnlockAsync(string password)
    {
        var success = await Read(db =>
        {
            var key = TryAuthenticate(db, password);
            if (key is null) return false;
            InstallSession(db, key); return true;
        });
        if (success) ProtectionChanged?.Invoke(this, EventArgs.Empty);
        return success;
    }
    public Task<byte[]?> TryWrapForDeviceAsync(string password, Func<byte[], byte[]> wrap) => Read(db =>
    {
        var key = TryAuthenticate(db, password);
        if (key is null) return null;
        try { return wrap(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    });

    public Task<PreparedCredentials?> PrepareCredentialsAsync(CredentialChange change, string credential, string? newPassword = null)
    {
        if (change != CredentialChange.RenewRecovery) NoteCipher.ValidatePassword(newPassword!);
        return Read(db =>
        {
            var previousPassword = Scalar(db, "SELECT PasswordKey FROM Protection WHERE Id=1") as byte[];
            var previousRecovery = Scalar(db, "SELECT RecoveryKey FROM Protection WHERE Id=1") as byte[];
            byte[]? key;
            if (change == CredentialChange.Configure)
            {
                if (previousPassword is not null) throw new InvalidOperationException("Protection is already configured.");
                key = SecretMemory.Random(32);
            }
            else if (change == CredentialChange.Recover)
            {
                if (previousRecovery is null) return null;
                byte[] recovery;
                try { recovery = NoteCipher.RecoveryKey(credential); }
                catch (ProtectionAuthenticationException) { return null; }
                try { key = TryUnwrap(recovery, previousRecovery, Context("vault", "recovery")); }
                finally { CryptographicOperations.ZeroMemory(recovery); }
            }
            else key = TryAuthenticate(db, credential);
            if (key is null) return null;
            try
            {
                // Renewal keeps the current password; it is rewrapped with a fresh salt under the rotated key.
                var salt = RandomNumberGenerator.GetBytes(32);
                var derived = NoteCipher.PasswordKey(change == CredentialChange.RenewRecovery ? credential : newPassword!, salt, NoteCipher.Iterations);
                var recovery = SecretMemory.Random(32);
                var prepared = new PreparedCredentials(NoteCipher.RecoveryCode(recovery), archiveId, SessionVersion,
                    key, salt, derived, recovery, previousPassword, previousRecovery);
                pendingCredentials.RemoveAll(p => p.Disposed);
                pendingCredentials.Add(prepared);
                return prepared;
            }
            catch { CryptographicOperations.ZeroMemory(key); throw; }
        });
    }

    public async Task CommitCredentialsAsync(PreparedCredentials prepared)
    {
        await InitializeAsync();
        await writer.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                if (prepared.Disposed || !pendingCredentials.Contains(prepared) || prepared.Session != SessionVersion || prepared.ArchiveId != archiveId)
                    throw new InvalidOperationException("Credential preparation is no longer valid.");
                using var db = Open();
                static bool Same(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && CryptographicOperations.FixedTimeEquals(a, b);
                byte[] master;
                using (var tx = db.BeginTransaction())
                {
                    if (!Same(prepared.PreviousPasswordKey, Scalar(db, "SELECT PasswordKey FROM Protection WHERE Id=1") as byte[]) ||
                        !Same(prepared.PreviousRecoveryKey, Scalar(db, "SELECT RecoveryKey FROM Protection WHERE Id=1") as byte[]))
                        throw new InvalidOperationException("Credentials changed during preparation.");
                    // Every change rotates the master key, so wrappers made before it open nothing current.
                    master = RekeyNotes(db, prepared.MasterKey);
                    Execute(db, "UPDATE Protection SET Salt=$salt,Iterations=$iterations,PasswordKey=$password,Verifier=$verifier,Maintenance=1 WHERE Id=1",
                        ("$salt", prepared.Salt), ("$iterations", NoteCipher.Iterations),
                        ("$password", NoteCipher.Encrypt(prepared.DerivedPasswordKey, master, Context("vault", "password"))),
                        ("$verifier", NoteCipher.EncryptText(master, archiveId, VerifierContext(db))));
                    StoreRecoveryWrappers(db, master, prepared.RecoverySecret);
                    Commit(db, tx, master);
                }
                IsProtectionConfigured = true;
                prepared.Dispose();
                try { Scrub(db); }
                catch (Exception ex) when (ex is IOException or SqliteException) { }
                InstallSession(db, master);
            });
        }
        finally { writer.Release(); }
        ProtectionChanged?.Invoke(this, EventArgs.Empty);
    }
    public Task CancelCredentialsAsync(PreparedCredentials prepared) => Read(db =>
    {
        pendingCredentials.Remove(prepared); prepared.Dispose(); return true;
    });
}

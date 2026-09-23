using MarkdownMkII.Services;
using MarkdownMkII.Storage;
using Microsoft.Data.Sqlite;
using System.Text;

namespace MarkdownMkII.App.Logic.Tests;

public sealed class RecoveryDocumentTests
{
    [Fact]
    public async Task DownloadIsPortableAndDoesNotActivatePreparedCredentials()
    {
        var root = Path.Combine(Path.GetTempPath(), "mkii-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var db = new NoteDatabase(Path.Combine(root, "notes.db"));
        try
        {
            using var prepared = await db.PrepareCredentialsAsync(CredentialChange.Configure, "", "test recovery passphrase");
            Assert.NotNull(prepared);
            var note = (await db.CreateAsync("Titolo: prova / note?", "body")).Summary;
            var name = RecoveryDocument.FileName(prepared);
            Assert.DoesNotContain(name, c => Path.GetInvalidFileNameChars().Contains(c));
            Assert.DoesNotContain("Titolo", name);
            var file = Path.Combine(root, name);
            await RecoveryDocument.WriteAsync(file, prepared);
            var bytes = await File.ReadAllBytesAsync(file);
            Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, bytes[..3]);
            var text = Encoding.UTF8.GetString(bytes);
            Assert.Contains(prepared.ArchiveId, text);
            Assert.DoesNotContain(note.Id, text);
            Assert.DoesNotContain(note.Title, text);
            Assert.Contains(prepared.RecoveryCode, text);
            Assert.Contains("RecoveryInstructions", text);
            Assert.False((await db.ProtectionSettingsAsync()).Configured);
            var error = await Assert.ThrowsAnyAsync<Exception>(() => RecoveryDocument.WriteAsync(root, prepared));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.False((await db.ProtectionSettingsAsync()).Configured);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RecoveryDocument.WriteAsync(file, prepared, cancelled.Token));
            await db.CancelCredentialsAsync(prepared);
        }
        finally { await db.LockAsync(); SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }
}

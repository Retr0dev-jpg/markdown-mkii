using Microsoft.Data.Sqlite;

namespace MarkdownMkII.Storage.Tests;

public sealed class CredentialsAndBulkTests : IDisposable
{
    private const string Password = "long test passphrase for archive";
    private const string Replacement = "replacement test passphrase for archive";
    private readonly string root = Path.Combine(Path.GetTempPath(), "mkii-bulk-" + Guid.NewGuid().ToString("N"));
    private NoteDatabase Database => new(Path.Combine(root, "notes.db"));
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Fact]
    public async Task PreparingAndCancellingDoesNotConfigureProtection()
    {
        var db = Database;
        using var pending = await db.PrepareCredentialsAsync(CredentialChange.Configure, "", Password);
        Assert.NotNull(pending);
        Assert.False((await db.ProtectionSettingsAsync()).Configured);
        Assert.False(await db.TryUnlockAsync(Password));
        await db.CancelCredentialsAsync(pending);
        Assert.Empty(pending.RecoveryCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.CommitCredentialsAsync(pending));
    }
    [Fact]
    public async Task RecoveryOnlyChangesCredentialsAtCommitAndLockInvalidatesPreparation()
    {
        var db = Database;
        var original = await db.ConfigureProtectionAsync(Password);
        Assert.False(await db.TryUnlockAsync("incorrect password"));
        Assert.Null(await db.PrepareCredentialsAsync(CredentialChange.Recover, "invalid", Replacement));
        using var pending = await db.PrepareCredentialsAsync(CredentialChange.Recover, original, Replacement);
        Assert.NotNull(pending);
        Assert.False(await db.TryUnlockAsync(Replacement));
        await db.LockAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.CommitCredentialsAsync(pending));
        using var next = await db.PrepareCredentialsAsync(CredentialChange.Recover, original, Replacement);
        Assert.NotNull(next);
        var newCode = next.RecoveryCode;
        await db.CommitCredentialsAsync(next);
        Assert.True(db.IsUnlocked);
        await db.LockAsync();
        Assert.False(await db.TryUnlockAsync(Password));
        Assert.True(await db.TryUnlockAsync(Replacement));
        Assert.Null(await db.PrepareCredentialsAsync(CredentialChange.Recover, original, Password));
        using var accepted = await db.PrepareCredentialsAsync(CredentialChange.Recover, newCode, Password);
        Assert.NotNull(accepted);
    }
    [Fact]
    public async Task StableIdSnapshotSurvivesChangesAndBulkPreservesUnrelatedTags()
    {
        var db = Database;
        var a = await db.CreateAsync("a", "one"); var b = await db.CreateAsync("b", "two");
        var ids = await db.QueryIdsAsync(new());
        await db.SaveAsync(a.Summary.Id, "updated", a.Version);
        var c = await db.CreateAsync("c");
        Assert.Equal(ids, (await db.SummariesAsync(ids)).Select(n => n.Id));
        Assert.DoesNotContain(c.Summary.Id, ids);
        await db.UpdateMetadataAsync(a.Summary.Id, "a", false, NoteColor.None, null, ["existing"]);
        var result = await db.ApplyBulkAsync(ids, new(BulkAction.AddTags, Tags: [" Shared ", "shared"]));
        Assert.Equal(2, result.ChangedIds.Count);
        Assert.Empty(result.FailedIds);
        Assert.Equal(2, (await db.SummaryAsync(a.Summary.Id))!.Tags.Count);
        Assert.Single((await db.SummaryAsync(b.Summary.Id))!.Tags);
        result = await db.ApplyBulkAsync(ids, new(BulkAction.AddTags, Tags: ["shared"]));
        Assert.Equal(2, result.Unchanged);
        Assert.Empty(result.ChangedIds);
        result = await db.ApplyBulkAsync(ids, new(BulkAction.RemoveTags, Tags: ["SHARED"]));
        Assert.Equal(["existing"], (await db.SummaryAsync(a.Summary.Id))!.Tags);
        Assert.Empty((await db.SummaryAsync(b.Summary.Id))!.Tags);
    }
    [Fact]
    public async Task BulkProtectionAndDuplicationKeepPrivateBodiesEncrypted()
    {
        var db = Database;
        await db.ConfigureProtectionAsync(Password);
        var note = await db.CreateAsync("secret", "unique private text");
        var result = await db.ApplyBulkAsync([note.Summary.Id], new(BulkAction.Protect));
        Assert.False(result.MaintenancePending);
        Assert.Empty(result.FailedIds);
        var copies = await db.ApplyBulkAsync([note.Summary.Id], new(BulkAction.Duplicate));
        var id = Assert.Single(copies.CreatedIds);
        Assert.True((await db.SummaryAsync(id))!.IsProtected);
        await db.LockAsync();
        await Assert.ThrowsAsync<NoteLockedException>(() => db.GetAsync(id));
        Assert.DoesNotContain("unique private text", System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(db.FilePath)));
    }
    [Fact]
    public async Task CancellationStopsBetweenCommittedChunksAndFailuresAreCounted()
    {
        var db = Database;
        var a = await db.CreateAsync("a"); var b = await db.CreateAsync("b");
        var ids = new[] { a.Summary.Id, b.Summary.Id };
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var result = await db.ApplyBulkAsync(ids, new(BulkAction.Favorite), token: cancelled.Token);
        Assert.Equal(2, result.NotExecuted);
        Assert.False((await db.SummaryAsync(a.Summary.Id))!.Favorite);
        result = await db.ApplyBulkAsync(ids, new(BulkAction.Category, CategoryId: "missing-category"));
        Assert.Equal(2, result.FailedIds.Count);
        Assert.Empty(result.ChangedIds);
        Assert.Null((await db.SummaryAsync(a.Summary.Id))!.CategoryId);
    }
    [Fact]
    public async Task RenewalKeepsOldCodeValidUntilCommitAndRejectsStalePreparation()
    {
        var db = Database;
        var original = await db.ConfigureProtectionAsync(Password);
        using var pending = await db.PrepareCredentialsAsync(CredentialChange.RenewRecovery, Password);
        using var competing = await db.PrepareCredentialsAsync(CredentialChange.RenewRecovery, Password);
        Assert.NotNull(pending); Assert.NotNull(competing);
        var newCode = pending.RecoveryCode;
        using var originalAccepted = await db.PrepareCredentialsAsync(CredentialChange.Recover, original, Replacement);
        Assert.NotNull(originalAccepted);
        await db.CommitCredentialsAsync(pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.CommitCredentialsAsync(competing));
        Assert.Null(await db.PrepareCredentialsAsync(CredentialChange.Recover, original, Replacement));
        using var newAccepted = await db.PrepareCredentialsAsync(CredentialChange.Recover, newCode, Replacement);
        Assert.NotNull(newAccepted);
    }

    private sealed class InlineProgress(Action<BulkProgress> report) : IProgress<BulkProgress>
    {
        public void Report(BulkProgress value) => report(value);
    }

    [Fact]
    public async Task CancellationAfterFirstChunkReportsOnlyCommittedNotes()
    {
        var db = Database;
        var ids = new List<string>();
        for (var i = 0; i < 105; i++) ids.Add((await db.CreateAsync("note " + i)).Summary.Id);
        using var cancellation = new CancellationTokenSource();
        var result = await db.ApplyBulkAsync(ids, new(BulkAction.Favorite),
            new InlineProgress(_ => cancellation.Cancel()), cancellation.Token);
        Assert.Equal(50, result.ChangedIds.Count);
        Assert.Equal(55, result.NotExecuted);
        Assert.Empty(result.FailedIds);
        Assert.Equal(50, (await db.SummariesAsync(ids)).Count(n => n.Favorite));
    }

    [Fact]
    public async Task LabelTargetsIncludeArchivedAndTrashedNotesWithoutBodies()
    {
        var db = Database;
        var category = await db.AddCategoryAsync("Work");
        var note = await db.CreateAsync("one", "body");
        await db.UpdateMetadataAsync(note.Summary.Id, "one", false, NoteColor.None, category, ["tag"]);
        await db.SetStateAsync(note.Summary.Id, true, true);
        var tag = Assert.Single(await db.LabelsAsync(false));
        Assert.Equal([note.Summary.Id], await db.LabelNoteIdsAsync(true, category));
        Assert.Equal([note.Summary.Id], await db.LabelNoteIdsAsync(false, tag.Id));
    }

}

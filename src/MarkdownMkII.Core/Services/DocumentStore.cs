using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Services;

public sealed class DocumentSnapshot
{
    public required string Path { get; init; }

    public required string Text { get; init; }

    public TextFileProfile Profile { get; init; } = TextFileProfile.Utf8Lf;

    public DateTimeOffset LoadedAt { get; init; } = DateTimeOffset.Now;

    public long FileSize { get; init; }

    public DateTimeOffset LastWriteTimeUtc { get; init; }
}

/// <summary>
/// Lettura/scrittura atomica dei file Markdown. Nessuna dipendenza WinRT: testabile su Linux.
/// </summary>
public sealed class DocumentStore
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd", ".txt"
    };

    public async Task<DocumentSnapshot> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var (text, profile) = TextFileCodec.Decode(bytes);
            var info = new FileInfo(path);
            return new DocumentSnapshot
            {
                Path = path,
                Text = text,
                Profile = profile,
                FileSize = info.Length,
                LastWriteTimeUtc = info.LastWriteTimeUtc
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveAsync(string path, string text, TextFileProfile? profile = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        text ??= string.Empty;
        profile ??= TextFileProfile.Utf8Lf;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() => WriteAtomic(path, text, profile), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveBackupAsync(string originalPath, string text, CancellationToken cancellationToken = default)
    {
        var backup = BackupPath(originalPath);
        await SaveAsync(backup, text, profile: null, cancellationToken).ConfigureAwait(false);
    }

    public static string BackupPath(string originalPath)
    {
        if (string.IsNullOrWhiteSpace(originalPath))
        {
            return Path.Combine(Path.GetTempPath(), "markdown-mkii-recovery.md.bak");
        }

        return originalPath + ".bak";
    }

    public static string RecoveryPath(string directory)
        => Path.Combine(directory, "unsaved-recovery.md.bak");

    public static string UniqueMarkdownPath(string directory, string stem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        stem = string.IsNullOrWhiteSpace(stem) ? "note" : stem.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(invalid, '-');
        }

        stem = stem
            .Replace(Path.DirectorySeparatorChar, '-')
            .Replace(Path.AltDirectorySeparatorChar, '-')
            .Replace("..", "-", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(stem) || stem is "." or "..")
        {
            stem = "note";
        }

        var path = Path.Combine(directory, stem + ".md");
        var index = 2;
        while (File.Exists(path) || Directory.Exists(path))
        {
            path = Path.Combine(directory, stem + " " + index + ".md");
            index++;
        }

        return path;
    }

    public static bool IsMarkdown(string path)
        => MarkdownExtensions.Contains(Path.GetExtension(path));

    public static void WriteAtomic(string path, string text, TextFileProfile? profile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        // Workspace rewrites must retain the encoding and line endings of an existing note.
        if (profile is null && File.Exists(path))
        {
            profile = TextFileCodec.Decode(File.ReadAllBytes(path)).Profile;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, TextFileCodec.Encode(text, profile));
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    public static bool IsNewerOnDisk(string path, DateTimeOffset knownUtc)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            return File.GetLastWriteTimeUtc(path) > knownUtc + TimeSpan.FromMilliseconds(750);
        }
        catch
        {
            return false;
        }
    }

    public static string NormalizeNewlines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort: un temp orfano non deve far fallire il chiamante.
        }
    }
}

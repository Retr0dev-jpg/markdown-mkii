using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Services.Localization;
using MarkdownMkII.Storage;
using System.Security.Cryptography;
using MarkdownMkII.Core.Preview;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

namespace MarkdownMkII.Services;
public static class ArchiveAssets
{
    internal static readonly SemaphoreSlim CacheGate = new(1, 1);
    private static string Id(string uri) => uri[(uri.IndexOf(':') + 1)..].TrimStart('/').Split('#')[0];

    public static bool IsAttachmentUri(string? uri)
        => uri is not null && (uri.StartsWith("attachment:", StringComparison.OrdinalIgnoreCase) || uri.StartsWith(PreviewBuilder.AttachmentNameScheme, StringComparison.OrdinalIgnoreCase));

    /// <summary>Turns an attachment-name reference into an <c>attachment://id</c> URI, or null when no attachment has that name.</summary>
    private static async Task<string?> ResolveAsync(string uri)
    {
        if (!uri.StartsWith(PreviewBuilder.AttachmentNameScheme, StringComparison.OrdinalIgnoreCase)) return uri;
        var parts = uri[PreviewBuilder.AttachmentNameScheme.Length..].Split('/', 2);
        if (parts.Length != 2) return null;
        var id = await NoteArchive.Database.FindAttachmentAsync(Uri.UnescapeDataString(parts[1]), Uri.UnescapeDataString(parts[0]));
        return id is null ? null : "attachment://" + id;
    }

    public static async Task LoadImageAsync(BitmapImage bitmap, string uri)
    {
        try
        {
            if (await ResolveAsync(uri) is not { } resolved) return;
            uri = resolved;
            var session = NoteArchive.Database.SessionVersion;
            using var stream = new MemoryStream();
            try
            {
                await NoteArchive.Database.CopyAttachmentAsync(Id(uri), stream, NoteSecurity.OperationsToken);
                if (session != NoteArchive.Database.SessionVersion || NoteSecurity.Blocking) return;
                stream.Position = 0;
                using var random = stream.AsRandomAccessStream();
                await bitmap.SetSourceAsync(random);
                if (session != NoteArchive.Database.SessionVersion || NoteSecurity.Blocking) bitmap.UriSource = null;
            }
            finally { if (stream.TryGetBuffer(out var bytes)) CryptographicOperations.ZeroMemory(bytes.AsSpan()); }
        }
        catch (Exception ex) when (ex is NoteLockedException or OperationCanceledException) { }
        catch (Exception ex)
        {
            DiagnosticsService.LogError("Archive", "Immagine non disponibile", ex);
        }
    }

    public static async Task OpenAsync(string uri)
    {
        if (await ResolveAsync(uri) is not { } resolved) return;
        var id = Id(resolved);
        var protectedAsset = await NoteArchive.Database.IsProtectedAttachmentAsync(id);
        if (protectedAsset && (!await NoteSecurity.EnsureUnlockedAsync() || !await NoteSecurity.ConfirmPlaintextAsync())) return;
        var token = NoteSecurity.OperationsToken;
        var name = await NoteArchive.Database.AttachmentNameAsync(id);
        if (name is null)
            return;
        if (protectedAsset)
        {
            var file = await FilePickerService.SaveAttachmentAsync(name);
            if (file is null) return;
            token.ThrowIfCancellationRequested();
            using (var output = File.Create(file.Path)) await NoteArchive.Database.CopyAttachmentAsync(id, output, token);
            token.ThrowIfCancellationRequested();
            await LocalFileLauncher.OpenAsync(file.Path);
            return;
        }
        await CacheGate.WaitAsync(token);
        try
        {
            if (NoteSecurity.Blocking) return;
            var folder = Path.Combine(AppPaths.Root, "attachment-cache");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, id + Path.GetExtension(name));
            if (!File.Exists(path))
            {
                using var output = File.Create(path);
                await NoteArchive.Database.CopyAttachmentAsync(id, output);
            }

            await LocalFileLauncher.OpenAsync(path);
        }
        finally { CacheGate.Release(); }
    }

    public static async Task<NotePreviewContext> PreviewContextAsync(string id, string markdown)
    {
        var session = NoteArchive.Database.SessionVersion;
        var embedded = new Dictionary<string, EmbeddedNote>(StringComparer.OrdinalIgnoreCase);
        embedded[id] = new(id, "", markdown);
        async Task Visit(string text, int depth)
        {
            if (depth >= 4 || embedded.Count >= 32)
                return;
            foreach (var link in WikiLinks.Find(text).Where(l => l.IsEmbed && !WikiIndex.IsMediaTarget(l.Target)))
            {
                if (embedded.ContainsKey(link.Target))
                    continue;
                var matches = await NoteArchive.Database.ResolveAsync(link.Target);
                if (matches.Count != 1)
                    continue;
                var summary = matches[0];
                if (embedded.TryGetValue(summary.Id, out var previous))
                {
                    embedded[link.Target] = previous;
                    continue;
                }

                if (summary.IsLocked)
                {
                    var placeholder = new EmbeddedNote(summary.Id, string.IsNullOrEmpty(summary.Title) ? Strings.T("SecurityProtectedNote") : summary.Title,
                        "[" + Strings.T("SecurityUnlock") + "](note://" + summary.Id + ")");
                    embedded[summary.Id] = placeholder;
                    embedded[link.Target] = placeholder;
                    continue;
                }
                var note = await NoteArchive.Database.GetAsync(summary.Id);
                if (session != NoteArchive.Database.SessionVersion || NoteSecurity.Blocking) throw new OperationCanceledException();
                if (note is null || note.Markdown.Length > 200_000)
                    continue;
                var resolved = new EmbeddedNote(summary.Id, summary.Title, note.Markdown);
                embedded[summary.Id] = resolved;
                embedded[link.Target] = resolved;
                await Visit(note.Markdown, depth + 1);
            }
        }

        await Visit(markdown, 0);
        if (session != NoteArchive.Database.SessionVersion || NoteSecurity.Blocking) throw new OperationCanceledException();
        return new(id, embedded);
    }
}

using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class DocumentStoreTests
{
    [Fact]
    public async Task WriteAtomic_Roundtrips_Utf8()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mkii-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "note.md");
            var store = new DocumentStore();
            await store.SaveAsync(path, "ciao\nmondo");
            var loaded = await store.LoadAsync(path);
            Assert.Equal("ciao\nmondo", loaded.Text);
            Assert.Equal(TextEncodingKind.Utf8, loaded.Profile.Encoding);
            Assert.True(DocumentStore.IsMarkdown(path));
            var stamp = loaded.LastWriteTimeUtc;
            Assert.False(DocumentStore.IsNewerOnDisk(path, stamp + TimeSpan.FromHours(1)));
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(2));
            Assert.True(DocumentStore.IsNewerOnDisk(path, stamp));
            await store.SaveAsync(path, "ciao\nmondo", new TextFileProfile(TextEncodingKind.Utf16Be, NewlineKind.Crlf));
            var again = await store.LoadAsync(path);
            Assert.Equal("ciao\nmondo", again.Text);
            Assert.Equal(TextEncodingKind.Utf16Be, again.Profile.Encoding);
            Assert.Equal(NewlineKind.Crlf, again.Profile.Newline);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

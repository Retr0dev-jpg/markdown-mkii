using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Services;
using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public sealed class MoveNoteTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "markdown-mkii-move-" + Guid.NewGuid().ToString("N"));

    public MoveNoteTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);

    private string Note(string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Relocate_SkipsOpenFilesAndUsesSameRewriteForUnsavedBuffers()
    {
        var source = Note("sub/A.md", "# A");
        var open = Note("B.md", "[[sub/A#heading|label]]");
        var closed = Note("C.md", "![[sub/A]]\n[A](sub/A.md#heading)");
        var destinationFolder = Directory.CreateDirectory(Path.Combine(root, "other")).FullName;

        var moved = WikiIndex.Relocate(source, destinationFolder, [root], [open]);

        Assert.NotNull(moved);
        Assert.False(File.Exists(source));
        Assert.Equal("[[sub/A#heading|label]]", File.ReadAllText(open));
        Assert.Equal("![[other/A]]\n[A](other/A.md#heading)", File.ReadAllText(closed));
        Assert.Equal("[[other/A#heading|label]]\nunsaved changes",
            WikiIndex.RewriteRelocatedLinks(File.ReadAllText(open) + "\nunsaved changes", open, source, moved, [root]));
    }

    [Fact]
    public void Relocate_FromVaultRootUpdatesRootTargetsAndKeepsFileProfile()
    {
        var source = Note("A.md", "# A");
        var referring = Note("B.md", "");
        var profile = new TextFileProfile(TextEncodingKind.Utf16Le, NewlineKind.Crlf);
        File.WriteAllBytes(referring, TextFileCodec.Encode("[[A]]\n[A](./A.md)\n", profile));
        var destinationFolder = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;

        WikiIndex.Relocate(source, destinationFolder, [root]);

        var decoded = TextFileCodec.Decode(File.ReadAllBytes(referring));
        Assert.Equal(profile, decoded.Profile);
        Assert.Equal("[[nested/A]]\n[A](./nested/A.md)\n", decoded.Text);
    }

    [Fact]
    public void Relocate_RewritesMarkdownPathsRelativeToEachReferringDocument()
    {
        var source = Note("sub/A.md", "# A");
        var referring = Note("notes/B.md", "[A](../sub/A.md)\n[unrelated](sub/A.md)");
        Note("notes/sub/A.md", "unrelated");
        var destinationFolder = Directory.CreateDirectory(Path.Combine(root, "other")).FullName;

        WikiIndex.Relocate(source, destinationFolder, [root]);

        Assert.Equal("[A](../other/A.md)\n[unrelated](sub/A.md)", File.ReadAllText(referring));
    }

    [Fact]
    public void Relocate_PreservesExtensionAndRewritesCollisionDestination()
    {
        var source = Note("A.markdown", "original");
        Note("other/A.markdown", "existing");
        var referring = Note("B.md", "[[A]]\n[[A.markdown]]\n[A](A.markdown)");

        var moved = WikiIndex.Relocate(source, Path.Combine(root, "other"), [root]);

        Assert.Equal(Path.Combine(root, "other", "A-2.markdown"), moved);
        Assert.Equal("original", File.ReadAllText(moved!));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(root, "other", "A.markdown")));
        Assert.Equal("[[other/A-2]]\n[[other/A-2.markdown]]\n[A](other/A-2.markdown)", File.ReadAllText(referring));
    }

    [Fact]
    public void Relocate_OutsideIndexedFolderKeepsWikilinkResolvable()
    {
        var vault = Directory.CreateDirectory(Path.Combine(root, "vault")).FullName;
        var source = Note("vault/A.md", "# A");
        var referring = Note("vault/B.md", "[[A]]\n[A](A.md)");
        var destinationFolder = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;

        var moved = WikiIndex.Relocate(source, destinationFolder, [vault]);

        var target = Path.ChangeExtension(moved, null)!.Replace('\\', '/');
        Assert.Equal($"[[{target}]]\n[A](../outside/A.md)", File.ReadAllText(referring));
        Assert.Equal(moved, WikiIndex.Resolve(target, referring, [vault]));
    }

    [Fact]
    public void FailedRelocation_LeavesFilesAndReferencesUntouched()
    {
        var source = Note("A.md", "original");
        var referring = Note("B.md", "[[A]]");

        Assert.Null(WikiIndex.Relocate(source, Path.Combine(root, "missing"), [root]));

        Assert.Equal("original", File.ReadAllText(source));
        Assert.Equal("[[A]]", File.ReadAllText(referring));
    }

    [Fact]
    public void ArchiveAndUnarchive_RewriteQualifiedLinksAndProtectOpenFiles()
    {
        var source = Note("sub/A.md", "original");
        var referring = Note("B.md", "[[sub/A]]\n[A](sub/A.md)");
        var open = Note("C.md", "[[sub/A]]");

        var archived = VaultAttachments.Archive(source, [root], [open]);

        Assert.Equal(Path.Combine(root, "archive", "A.md"), archived);
        Assert.Equal("[[archive/A]]\n[A](archive/A.md)", File.ReadAllText(referring));
        Assert.Equal("[[sub/A]]", File.ReadAllText(open));
        var openBuffer = WikiIndex.RewriteRelocatedLinks("[[sub/A]]\nunsaved", open, source, archived!, [root]);
        Assert.Equal("[[archive/A]]\nunsaved", openBuffer);

        var restored = VaultAttachments.Unarchive(archived!, [root], [open]);

        Assert.Equal(Path.Combine(root, "A.md"), restored);
        Assert.Equal("[[A]]\n[A](A.md)", File.ReadAllText(referring));
        Assert.Equal("[[sub/A]]", File.ReadAllText(open));
        Assert.Equal("[[A]]\nunsaved", WikiIndex.RewriteRelocatedLinks(openBuffer, open, archived!, restored!, [root]));
    }

    [Fact]
    public void ArchiveAndUnarchive_PreserveExtensionAndUniqueNameSemantics()
    {
        var source = Note("A.markdown", "original");
        Note("archive/A.markdown", "existing archived");
        var archived = VaultAttachments.Archive(source, [root]);
        Assert.Equal(Path.Combine(root, "archive", "A-2.markdown"), archived);
        Note("A-2.markdown", "existing root");

        var restored = VaultAttachments.Unarchive(archived!, [root]);

        Assert.Equal(Path.Combine(root, "A-2-2.markdown"), restored);
        Assert.Equal("original", File.ReadAllText(restored!));
        Assert.Equal("existing archived", File.ReadAllText(Path.Combine(root, "archive", "A.markdown")));
        Assert.Equal("existing root", File.ReadAllText(Path.Combine(root, "A-2.markdown")));
    }

    [Fact]
    public void ArchiveAndUnarchive_RebaseMovedDocumentAndItsQualifiedSelfLink()
    {
        var source = Note("sub/A.md", "[[sub/A]]\n[B](../B.md)\n![picture](images/p.png)");
        Note("B.md", "# B");

        var archived = VaultAttachments.Archive(source, [root]);

        Assert.Equal("[[archive/A]]\n[B](../B.md)\n![picture](../sub/images/p.png)", File.ReadAllText(archived!));
        var restored = VaultAttachments.Unarchive(archived!, [root]);
        Assert.Equal("[[A]]\n[B](B.md)\n![picture](sub/images/p.png)", File.ReadAllText(restored!));
    }

    [Fact]
    public void Relocate_ProtectsDirtySourceOnDiskWhileRebasingItsBuffer()
    {
        const string original = "[[sub/A]]\n[B](../B.md)";
        var source = Note("sub/A.md", original);
        var destinationFolder = Directory.CreateDirectory(Path.Combine(root, "other", "deep")).FullName;

        var moved = WikiIndex.Relocate(source, destinationFolder, [root], [source]);

        Assert.Equal(original, File.ReadAllText(moved!));
        Assert.Equal("[[other/deep/A]]\n[B](../../B.md)\nunsaved",
            WikiIndex.RewriteRelocatedLinks(original + "\nunsaved", source, source, moved!, [root]));
    }
}

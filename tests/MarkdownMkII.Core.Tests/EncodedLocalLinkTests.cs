using MarkdownMkII.Core.Markdown;
using MarkdownMkII.Core.Preview;

namespace MarkdownMkII.Core.Tests;

public sealed class EncodedLocalLinkTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "mkii-encoded-links-" + Guid.NewGuid().ToString("N"));

    public EncodedLocalLinkTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public void MovedNoteWithEncodedImageAndMarkdownLinksRemainsResolvable()
    {
        var note = Path.Combine(root, "note one.md");
        var image = Path.Combine(root, "image (1).png");
        File.WriteAllText(note, "# Heading");
        File.WriteAllText(image, "image");
        var oldPath = Path.Combine(root, "A.md");
        var newPath = Path.Combine(root, "nested", "A.md");
        var movedText = MarkdownLinkRelocation.Rebase("[note](<note one.md>) ![image](image%20%281%29.png)", oldPath, newPath);
        var preview = PreviewBuilder.Build(movedText, newPath, [root]);
        var links = Assert.IsType<ParagraphBlockIr>(Assert.Single(preview.Blocks)).Inlines.OfType<LinkInline>().ToArray();
        Assert.Equal(note, links[0].Url);
        Assert.Equal(image, links[1].Url);
        Assert.Equal(image, LinkResolver.TryResolveLocal("../image%20%281%29.png?raw=1#caption", newPath));
    }

    [Fact]
    public void LiteralHashFilenameIsKeptSeparateFromHeadingFragment()
    {
        var note = Path.Combine(root, "note#one.md");
        File.WriteAllText(note, "# Heading");
        var preview = PreviewBuilder.Build("[note](note%23one.md#heading)", Path.Combine(root, "A.md"), [root]);
        var link = Assert.IsType<LinkInline>(Assert.Single(Assert.IsType<ParagraphBlockIr>(Assert.Single(preview.Blocks)).Inlines));
        var (url, heading) = LinkResolver.SplitFragment(link.Url);
        Assert.Equal("heading", heading);
        Assert.Equal(note, new Uri(url).LocalPath);
        Assert.Equal(note, LinkResolver.TryResolveLocal(link.Url, null));
    }

    [Fact]
    public void ArchiveNoteImagesResolveByAttachmentNameInsteadOfNoteLinks()
    {
        var notes = new NotePreviewContext("n1", new Dictionary<string, EmbeddedNote>());
        var preview = PreviewBuilder.Build("![a](./img/shot%201.png) [b](Other) ![[clip.png]]", notes: notes);
        var links = Assert.IsType<ParagraphBlockIr>(Assert.Single(preview.Blocks)).Inlines.OfType<LinkInline>().ToArray();
        Assert.Equal(PreviewBuilder.AttachmentNameScheme + "n1/shot%201.png", links[0].Url);
        Assert.StartsWith("wiki://", links[1].Url);
        Assert.Equal(PreviewBuilder.AttachmentNameScheme + "n1/clip.png", links[2].Url);
    }

    [Fact]
    public void FileUriAndLiteralPercentFilenameDecodeExactlyOnce()
    {
        var image = Path.Combine(root, "image%20one.png");
        File.WriteAllText(image, "image");
        Assert.Equal(image, LinkResolver.TryResolveLocal("image%2520one.png", Path.Combine(root, "A.md")));
        Assert.Equal(image, LinkResolver.TryResolveLocal(new Uri(image).AbsoluteUri, null));
    }
}

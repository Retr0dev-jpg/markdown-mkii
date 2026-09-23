using MarkdownMkII.Core.Markdown;

namespace MarkdownMkII.Core.Tests;

public sealed class MarkdownLinkRelocationTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "markdown-relocation-tests"));
    private static string At(string relative) => Path.Combine(Root, relative);

    [Fact]
    public void MovingIntoSubfolder_RebasesNotesImagesAndPreservesTitles()
    {
        const string text = "[B](B.md#heading \"Title\") ![image](images/x.png 'Image title')";
        Assert.Equal("[B](../B.md#heading \"Title\") ![image](../images/x.png 'Image title')",
            MarkdownLinkRelocation.Rebase(text, At("A.md"), At("other/A.md")));
    }

    [Fact]
    public void MovingAcrossFolders_RebasesReferenceDefinitionsOnceAndKeepsLabels()
    {
        const string text = "[one][note] [two][note]\n\n[note]: <../B.md> \"Title\"\n[unused]: ../images/x.png\n";
        Assert.Equal("[one][note] [two][note]\n\n[note]: <../../B.md> \"Title\"\n[unused]: ../../images/x.png\n",
            MarkdownLinkRelocation.Rebase(text, At("notes/A.md"), At("other/deep/A.md")));
    }

    [Fact]
    public void SelfLinks_FollowTheMovedOrRenamedDocument()
    {
        const string text = "[self](A.md#section) [other](B.md) [anchor](#section)";
        Assert.Equal("[self](Renamed.md#section) [other](../B.md) [anchor](#section)",
            MarkdownLinkRelocation.Rebase(text, At("A.md"), At("other/Renamed.md")));
        Assert.Equal("[self](Renamed.md#section) [other](B.md) [anchor](#section)",
            MarkdownLinkRelocation.Rebase(text, At("A.md"), At("Renamed.md")));
    }

    [Fact]
    public void CodeProseWikiLinksAndExternalDestinationsRemainUntouched()
    {
        const string text = "`[code](B.md)`\n\n```md\n![code](images/x.png)\n```\n\n    [indented](B.md)\n\n[[Wiki]] ![[picture.png]]\n\n[https](https://example.test/B.md) [mail](mailto:a@example.test) [network](//example.test/B.md) [anchor](#head) [query](?mode=1)\n\nThe prose B.md remains unchanged.";
        Assert.Equal(text, MarkdownLinkRelocation.Rebase(text, At("A.md"), At("other/A.md")));
    }

    [Fact]
    public void EscapedAndPointyDestinationsKeepValidMarkdownAndQuerySuffixes()
    {
        const string text = "[space](<folder/file name.md> 'Title') ![paren](images/a\\(b\\).png) [query](B.md?raw=1&amp;mode=2#heading)";
        Assert.Equal("[space](<../folder/file%20name.md> 'Title') ![paren](../images/a%28b%29.png) [query](../B.md?raw=1&amp;mode=2#heading)",
            MarkdownLinkRelocation.Rebase(text, At("A.md"), At("other/A.md")));
    }

    [Fact]
    public void ReferenceLabelsAndImageInsideLinkAreNotReformatted()
    {
        const string text = "[![label](images/x.png)](B.md)\r\n\r\n[Ref]: B.md\r\n[REF][] [Ref]\r\n";
        Assert.Equal("[![label](../images/x.png)](../B.md)\r\n\r\n[Ref]: ../B.md\r\n[REF][] [Ref]\r\n",
            MarkdownLinkRelocation.Rebase(text, At("A.md"), At("other/A.md")));
    }

    [Fact]
    public void SameLocation_DoesNotNormalizeOriginalDestinationSpelling()
    {
        const string text = "[file](./folder/a%20b.md)";
        Assert.Equal(text, MarkdownLinkRelocation.Rebase(text, At("A.md"), At("A.md")));
    }

    [Fact]
    public void MovingBetweenWindowsDrives_PreservesTheAbsoluteDrivePrefix()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string text = "[B](B%20note.md#heading) [self](A.md)";
        Assert.Equal("[B](C:/notes/B%20note.md#heading) [self](Renamed.md)",
            MarkdownLinkRelocation.Rebase(text, @"C:\notes\A.md", @"D:\other\Renamed.md"));
    }

    [Fact]
    public void MovingFromUncShare_PreservesTheNetworkRootAndEscapesPathSegments()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string text = "![image](images/picture%20one.png)";
        Assert.Equal("![image](//server/share%20name/notes/images/picture%20one.png)",
            MarkdownLinkRelocation.Rebase(text, @"\\server\share name\notes\A.md", @"C:\notes\A.md"));
    }
}

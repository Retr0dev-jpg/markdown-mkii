using MarkdownMkII.Core.Services;

namespace MarkdownMkII.Core.Tests;

public sealed class LibraryCatalogTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "MarkdownMkII.LibraryTests", Guid.NewGuid().ToString("N"));
    private static readonly Dictionary<string, DateTimeOffset> NoRecents = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> NoStars = new(StringComparer.OrdinalIgnoreCase);

    public LibraryCatalogTests() => Directory.CreateDirectory(directory);
    private string Note(string relative, string content)
    {
        var path = Path.GetFullPath(Path.Combine(directory, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void CardsUseFrontMatterAndReadableBodyInsteadOfFileNames()
    {
        Note("vault/123.md", "---\ntitle: Idee per domani\ndescription: Un progetto da sviluppare.\ntags: [design, appunti]\n---\n# Heading\nBody #windows");
        Note("vault/456.md", "# Titolo della nota\n\n```cs\nConsole.WriteLine();\n```\n\nUn paragrafo utile.");
        var catalog = LibraryCatalog.Scan([Path.Combine(directory, "vault")]);
        var first = Assert.Single(catalog.Notes, n => n.Title == "Idee per domani");
        Assert.Equal("Un progetto da sviluppare.", first.Description);
        Assert.Contains("design", first.Tags);
        Assert.Contains("windows", first.Tags);
        var second = Assert.Single(catalog.Notes, n => n.Title == "Titolo della nota");
        Assert.Equal("Un paragrafo utile.", second.Description);
    }

    [Fact]
    public void OverlappingRootsAreDeduplicatedAndArchiveIsSeparate()
    {
        var active = Note("vault/projects/active.md", "# Active");
        var archived = Note("vault/archive/old.md", "# Old");
        var root = Path.Combine(directory, "vault");
        var catalog = LibraryCatalog.Scan([Path.Combine(root, "archive"), root, Path.Combine(root, "projects")]);
        Assert.Equal(2, catalog.Notes.Count);
        Assert.Equal(3, catalog.Folders.Count);
        Assert.Equal(active, Assert.Single(LibraryCatalog.Select(catalog, LibraryCollection.All, NoRecents, NoStars)).Path);
        Assert.Equal(archived, Assert.Single(LibraryCatalog.Select(catalog, LibraryCollection.Archive, NoRecents, NoStars)).Path);
    }

    [Fact]
    public void ExternalRecentAndStarredFilesRemainAvailableWithoutEnteringAllNotes()
    {
        var a = Note("outside/recent.md", "# Recent");
        var b = Note("outside/favorite.md", "# Favorite");
        var catalog = LibraryCatalog.Scan([], [a, b, a, Path.Combine(directory, "missing.md")]);
        var recents = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase) { [a] = DateTimeOffset.Now };
        var stars = new HashSet<string>([b], StringComparer.OrdinalIgnoreCase);
        Assert.Empty(LibraryCatalog.Select(catalog, LibraryCollection.All, recents, stars));
        Assert.Equal(a, Assert.Single(LibraryCatalog.Select(catalog, LibraryCollection.Recent, recents, stars)).Path);
        Assert.Equal(b, Assert.Single(LibraryCatalog.Select(catalog, LibraryCollection.Starred, recents, stars)).Path);
    }

    [Fact]
    public void FolderScopeIncludesDescendantsButNotFoldersWithSimilarNames()
    {
        var a = Note("vault/work/child/a.md", "# A");
        Note("vault/workshop/b.md", "# B");
        var catalog = LibraryCatalog.Scan([Path.Combine(directory, "vault")]);
        var result = LibraryCatalog.Select(catalog, LibraryCollection.All, NoRecents, NoStars, Path.Combine(directory, "vault/work"));
        Assert.Equal(a, Assert.Single(result).Path);
    }

    [Fact]
    public void RecentOrderUsesOpeningTimeAndTitleSortCanOverrideIt()
    {
        var a = Note("vault/a.md", "# Alpha");
        var b = Note("vault/b.md", "# Beta");
        var catalog = LibraryCatalog.Scan([Path.Combine(directory, "vault")]);
        var recents = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase)
        { [a] = DateTimeOffset.Now.AddDays(-2), [b] = DateTimeOffset.Now };
        Assert.Equal(new[] { b, a }, LibraryCatalog.Select(catalog, LibraryCollection.Recent, recents, NoStars).Select(n => n.Path));
        Assert.Equal(new[] { a, b }, LibraryCatalog.Select(catalog, LibraryCollection.Recent, recents, NoStars, sort: LibrarySort.Title).Select(n => n.Path));
    }

    [Fact]
    public void SearchFindsMetadataAndContentBeyondTheCardSampleWithSourceLine()
    {
        var path = Note("vault/file.md", "# Un titolo personale\n" + new string('x', 70000) + "\nNeedle in the full document\n");
        var catalog = LibraryCatalog.Scan([Path.Combine(directory, "vault")]);
        Assert.Equal(path, Assert.Single(LibraryCatalog.Search(catalog.Notes, "titolo personale")).Path);
        var match = Assert.Single(LibraryCatalog.Search(catalog.Notes, "needle document"));
        Assert.Equal(3, match.Line);
        Assert.Contains("Needle", match.Preview);
        Assert.Empty(LibraryCatalog.Search(catalog.Notes, "does not exist"));
        Assert.Empty(LibraryCatalog.Search(catalog.Notes, Path.GetFileName(directory)));
    }

    [Fact]
    public void CancelledRequestsDoNotContinueScanningOrSearching()
    {
        Note("vault/note.md", "# Note");
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => LibraryCatalog.Scan([directory], cancellationToken: source.Token));
        var catalog = LibraryCatalog.Scan([directory]);
        Assert.Throws<OperationCanceledException>(() => LibraryCatalog.Search(catalog.Notes, "missing", source.Token));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}

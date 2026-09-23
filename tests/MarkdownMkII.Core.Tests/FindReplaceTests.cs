using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class FindReplaceTests
{
    [Fact]
    public void Finds_Wraps_And_Replaces()
    {
        var text = "Alpha alpha ALPHA";
        var options = new FindReplaceOptions { MatchCase = false, WrapAround = true };
        var all = FindReplace.FindAll(text, "alpha", options);
        Assert.Equal(3, all.Count);
        var previous = FindReplace.FindPrevious(text, "alpha", 6, options);
        Assert.NotNull(previous);
        Assert.Equal(0, previous.Value.Start);
        var (replaced, count) = FindReplace.ReplaceAll(text, "alpha", "beta", options);
        Assert.Equal(3, count);
        Assert.DoesNotContain("alpha", replaced, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Regex_Finds_And_Invalid_Pattern_Is_Empty()
    {
        var matches = FindReplace.FindAll("a1 b22 c", @"\d+", new FindReplaceOptions { UseRegex = true });
        Assert.Equal(2, matches.Count);
        var invalid = FindReplace.FindAll("abc", "(", new FindReplaceOptions { UseRegex = true });
        Assert.Empty(invalid);
        var (replaced, count) = FindReplace.ReplaceAll("a1 b22", @"(\d+)", "N$1", new FindReplaceOptions { UseRegex = true });
        Assert.Equal(2, count);
        Assert.Equal("aN1 bN22", replaced);
    }

    [Fact]
    public void WholeWord_Skips_Substrings()
    {
        var matches = FindReplace.FindAll("cat catalog", "cat", new FindReplaceOptions { WholeWord = true });
        Assert.Single(matches);
        Assert.Equal(0, matches[0].Start);
    }

    [Fact]
    public void Restricts_To_Selection_Range()
    {
        var text = "alpha beta alpha";
        var options = new FindReplaceOptions { RangeStart = 6, RangeLength = 4 };
        var matches = FindReplace.FindAll(text, "alpha", options);
        Assert.Empty(matches);
        var inRange = FindReplace.FindAll(text, "beta", options);
        Assert.Single(inRange);
        var (replaced, count) = FindReplace.ReplaceAll(text, "a", "X", new FindReplaceOptions { RangeStart = 0, RangeLength = 5 });
        Assert.Equal(2, count);
        Assert.StartsWith("XlphX", replaced);
        Assert.Contains("beta", replaced, StringComparison.Ordinal);
    }
}

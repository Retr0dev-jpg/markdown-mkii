using MarkdownMkII.Core.Text;

namespace MarkdownMkII.Core.Tests;

public class WordStatsTests
{
    [Fact]
    public void Counts_Words_And_Selection()
    {
        var stats = WordStats.Compute("uno due tre", 0, 3);
        Assert.Equal(3, stats.Words);
        Assert.Equal(1, stats.SelectedWords);
        Assert.Equal(11, stats.Characters);
        Assert.True(stats.ReadingTime >= TimeSpan.FromMinutes(1));
    }
}

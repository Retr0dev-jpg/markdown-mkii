using MarkdownMkII.Core.Markdown;

namespace MarkdownMkII.Core.Tests;

public class WikiSuggestTests
{
    [Fact]
    public void PartialTarget_And_Complete_Preserve_Suffix()
    {
        Assert.Equal("no", WikiSuggest.PartialTarget("See [[no", 8));
        Assert.Equal("no", WikiSuggest.PartialTarget("See [[no|L]]", 8));
        Assert.Null(WikiSuggest.PartialTarget("See [[no]] x", 12));

        var open = WikiSuggest.Complete("See [[no", 8, "Note");
        Assert.Equal("See [[Note]]", open.Text);

        var labeled = WikiSuggest.Complete("See [[no|L]]", 8, "Note");
        Assert.Equal("See [[Note|L]]", labeled.Text);

        var ranked = WikiSuggest.Filter(["Alpha", "Note", "Notebook"], "no");
        Assert.Equal("Note", WikiSuggest.Pick(ranked, "no"));
        Assert.Equal("Notebook", WikiSuggest.Pick(["Notebook", "Note"], "Notebook"));
    }
}

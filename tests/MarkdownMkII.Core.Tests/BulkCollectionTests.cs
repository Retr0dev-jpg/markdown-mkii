using MarkdownMkII.Core.Models;

namespace MarkdownMkII.Core.Tests;

public class BulkCollectionTests
{
    [Fact]
    public void ReplaceAll_Resets_Once()
    {
        var collection = new BulkObservableCollection<string> { "a" };
        var resets = 0;
        collection.CollectionChanged += (_, args) =>
        {
            if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                resets++;
            }
        };
        collection.ReplaceAll(["b", "c"]);
        Assert.Equal(1, resets);
        Assert.Equal(["b", "c"], collection);
    }
}

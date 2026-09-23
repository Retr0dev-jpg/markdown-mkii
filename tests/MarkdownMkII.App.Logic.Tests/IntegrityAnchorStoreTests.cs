using MarkdownMkII.Services.Interop;
using Microsoft.Win32;

namespace MarkdownMkII.App.Logic.Tests;

public sealed class IntegrityAnchorStoreTests
{
    [Fact]
    public void AnchorRoundTripsProtectedAndCanBeForgotten()
    {
        if (!OperatingSystem.IsWindows()) return;
        var store = new IntegrityAnchorStore();
        var archive = "test-" + Guid.NewGuid().ToString("N");
        var value = Enumerable.Range(0, 72).Select(i => (byte)i).ToArray();
        try
        {
            Assert.Null(store.Load(archive));
            store.Store(archive, value);
            Assert.Equal(value, store.Load(archive));
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Markdown MkII\Integrity"))
                Assert.NotEqual(value, (byte[])key!.GetValue(archive)!);
        }
        finally { store.Forget(archive); }
        Assert.Null(store.Load(archive));
    }
}

using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Tests.Shell;

public class ListPageItemsTests
{
    [Fact]
    public void ReplaceAll_keeps_the_same_collection_and_skips_an_identical_page()
    {
        var items = new ListPageItems<string>();
        var resets = 0;
        items.CollectionChanged += (_, _) => resets++;

        items.ReplaceAll(["a", "b"]);
        Assert.Equal(1, resets);
        Assert.Equal(["a", "b"], items);

        items.ReplaceAll(["a", "b"]);
        Assert.Equal(1, resets);

        items.ReplaceAll(["b", "a"]);
        Assert.Equal(2, resets);
        Assert.Equal(["b", "a"], items);
    }
}

using Avalonia.Headless.XUnit;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Shell;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Shell;

/// <summary>PageHost keeps a view for every page it is not told to drop: each closed sub-page must be released unless it is reused.</summary>
[Collection(UiTestCollection.Name)]
public class SubPageReleaseTests : UiTestBase
{
    [AvaloniaFact]
    public async Task A_new_order_details_page_is_released_when_it_closes()
    {
        using var app = Admin();
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        var released = new List<ViewModelBase>();
        shell.PageDiscarded += released.Add;

        // Order details is a reusable page type, but this instance is new and nobody keeps it.
        Assert.True(await app.Navigation.OpenAsync<OrderDetailsViewModel>(page => page.OrderId = Guid.NewGuid()));
        var opened = shell.SubPage;
        Assert.True(await shell.CloseSubPageAsync());

        Assert.Same(opened, Assert.Single(released));
    }

    [AvaloniaFact]
    public async Task A_reused_order_details_page_keeps_its_view_when_it_closes()
    {
        using var app = Admin();
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        var released = new List<ViewModelBase>();
        shell.PageDiscarded += released.Add;
        var details = Assert.IsType<OrderDetailsViewModel>(app.Navigation.CreatePage(typeof(OrderDetailsViewModel)));

        Assert.True(await app.Navigation.OpenAsync(details, page => page.OrderId = Guid.NewGuid()));
        Assert.True(await shell.CloseSubPageAsync());

        Assert.Empty(released);
    }
}

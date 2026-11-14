using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.Views;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.Views;

[Collection(UiTestCollection.Name)]
public class ViewWarmUpTests : UiTestBase
{
    [AvaloniaFact]
    public void Every_warm_up_view_builds_without_data_and_leaves_nothing_behind()
    {
        using var app = new TestServices();
        var layer = new Panel { Opacity = 0, IsHitTestVisible = false, DataContext = null };
        app.Show(new Panel { Children = { layer } });

        var types = ViewWarmUp.ViewModelTypes();
        Assert.Equal(typeof(PortalShellViewModel), types[0]);
        Assert.Contains(typeof(NewOrderViewModel), types);
        foreach (var portal in AppRoutes.Table.Portals)
        {
            Assert.All(portal.Pages, p => Assert.Contains(p.PageType, types));
        }

        foreach (var type in types)
        {
            ViewWarmUp.BuildOnce(layer, type);
        }

        Assert.Empty(layer.Children);
    }

    [AvaloniaFact]
    public async Task Warm_up_stops_when_someone_signs_in()
    {
        using var app = new TestServices();
        var layer = new Panel { Opacity = 0, IsHitTestVisible = false, DataContext = null };
        app.Show(new Panel { Children = { layer } });
        var (delay, built) = (ViewWarmUp.StartDelay, 0);
        ViewWarmUp.StartDelay = TimeSpan.Zero;
        ViewWarmUp.Reset();
        try
        {
            layer.Children.CollectionChanged += (_, e) =>
            {
                if (e.NewItems is { Count: > 0 })
                {
                    built++;
                    app.SignInAs(Roles.Admin);
                }
            };

            await ViewWarmUp.RunAsync(layer, () => !app.Session.IsAuthenticated);

            Assert.Equal(1, built);
            Assert.Empty(layer.Children);
        }
        finally
        {
            ViewWarmUp.StartDelay = delay;
        }
    }
}

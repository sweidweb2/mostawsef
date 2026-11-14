using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.Views.Admin;
using Mostawsef.Desktop.Views.Shell;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Shell;

[Collection(UiTestCollection.Name)]
public class MenuPrebuildTests : UiTestBase
{
    [AvaloniaFact]
    public async Task Menu_pages_are_built_in_idle_time_and_load_data_only_when_opened()
    {
        var (enabled, delay) = (PortalShellView.PrebuildMenuPages, PortalShellView.PrebuildDelay);
        PortalShellView.PrebuildMenuPages = true;
        PortalShellView.PrebuildDelay = TimeSpan.Zero;
        try
        {
            using var app = Admin();
            app.Navigation.NavigateToHome();
            var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
            var host = new DialogHost { Content = new ContentControl { Content = shell } };
            app.Get<DialogService>().Attach(host);
            var window = app.Show(host);

            await WaitUntil(() => window.GetVisualDescendants().OfType<PatientsView>().Any()
                && window.GetVisualDescendants().OfType<OrdersView>().Any()
                && window.GetVisualDescendants().OfType<LabTestsView>().Any());

            // Built and styled, but hidden and without data: nothing was requested yet.
            var prebuilt = window.GetVisualDescendants().OfType<PatientsView>().Single();
            Assert.False(prebuilt.IsVisible);
            Assert.True(prebuilt.GetVisualDescendants().OfType<DataGrid>().Any());
            Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, PatientsPath));

            shell.SelectedMenuIndex = shell.MenuItems.ToList().FindIndex(p => p.PageType == typeof(PatientsViewModel));
            var patients = Assert.IsType<PatientsViewModel>(shell.CurrentPage);
            await WaitUntil(() => !patients.IsLoading && patients.Items.Count > 0);

            Assert.Same(prebuilt, window.GetVisualDescendants().OfType<PatientsView>().Single());
            Assert.True(prebuilt.IsVisible);
            Assert.Single(app.Api.RequestsTo(HttpMethod.Get, PatientsPath));
        }
        finally
        {
            (PortalShellView.PrebuildMenuPages, PortalShellView.PrebuildDelay) = (enabled, delay);
        }
    }

    [AvaloniaTheory]
    [InlineData(800, true, 16)]
    [InlineData(1400, false, 24)]
    public async Task A_narrow_content_area_tightens_the_page_and_hides_compact_items(double width, bool narrow, double padding)
    {
        using var app = Admin();
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        var window = app.Show(new ContentControl { Content = shell });
        window.Width = width;
        await WaitUntil(() => window.GetVisualDescendants().OfType<Control>().Any(c => c.Classes.Contains("page") && c.IsEffectivelyVisible));

        var appShell = window.GetVisualDescendants().OfType<AppShell>().Single();
        var page = window.GetVisualDescendants().OfType<Control>().First(c => c.Classes.Contains("page") && c.IsEffectivelyVisible);
        var compactOnly = window.GetVisualDescendants().OfType<Control>().First(c => c.Classes.Contains("hide-compact"));

        Assert.Equal(narrow, appShell.Classes.Contains(":narrow"));
        Assert.Equal(padding, page.Margin.Left);
        Assert.Equal(!narrow, compactOnly.IsVisible);
    }
}

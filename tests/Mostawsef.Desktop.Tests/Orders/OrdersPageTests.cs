using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.Views.Admin;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Orders;

[Collection(UiTestCollection.Name)]
public class OrdersPageTests : UiTestBase
{
    private static readonly Type[] OrderPageTypes = [typeof(OrdersViewModel), typeof(NewOrderViewModel), typeof(OrderDetailsViewModel)];

    [AvaloniaFact]
    public void Only_admins_can_open_the_orders_pages()
    {
        using var app = new TestServices();
        Assert.All(OrderPageTypes, type => Assert.False(app.Navigation.CanAccess(type)));

        app.SignInAs(Roles.Super);
        Assert.All(OrderPageTypes, type => Assert.False(app.Navigation.CanAccess(type)));

        app.SignInAs(Roles.Admin);
        Assert.All(OrderPageTypes, type => Assert.True(app.Navigation.CanAccess(type)));
    }

    [AvaloniaFact]
    public async Task Super_cannot_open_an_order_sub_page_from_its_portal()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        var page = shell.CurrentPage;

        Assert.False(await app.Navigation.OpenAsync<NewOrderViewModel>());
        Assert.False(await app.Navigation.OpenAsync<OrderDetailsViewModel>(p => p.OrderId = Guid.NewGuid()));

        Assert.Same(page, shell.CurrentPage);
        Assert.Null(shell.SubPage);
    }

    [AvaloniaFact]
    public async Task The_list_shows_each_order_with_its_translated_status_and_the_record_count()
    {
        using var app = Admin();

        var (_, orders, window) = await ShowOrdersPageAsync(app);

        Assert.Equal(2, orders.Items.Count);
        var texts = Texts(window);
        Assert.Contains("ORD-000007", texts);
        Assert.Contains("Mona Adel", texts);
        Assert.Contains("010 1234 5678", texts);
        Assert.Contains("2 orders", texts);
        var badges = window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).ToDictionary(b => b.Text!, b => b.Kind);
        Assert.Equal(BadgeKind.Info, badges["Partially paid"]);
        Assert.Equal(BadgeKind.Warning, badges["Pending"]);
        Assert.Equal(2, window.GetVisualDescendants().OfType<IconButton>().Count(b => b.Command == orders.ViewCommand));
    }

    [AvaloniaFact]
    public async Task View_opens_the_details_in_the_portal_and_back_returns_to_the_same_list()
    {
        using var app = Admin();
        app.Api.OnGet($"{OrdersPath}/{PartlyPaid.Id}", Details(PartlyPaid.Id));
        var (shell, orders, window) = await ShowOrdersPageAsync(app);
        var menu = shell.SelectedMenuIndex;
        var listReads = ListReads(app);

        await orders.ViewCommand.ExecuteAsync(PartlyPaid);
        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasOrder);
        Dispatcher.UIThread.RunJobs();
        var detailsView = window.GetVisualDescendants().OfType<OrderDetailsView>().Single(v => v.IsVisible);

        await details.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        app.Api.OnGet($"{OrdersPath}/{Unpaid.Id}", Details(Unpaid.Id));
        await orders.ViewCommand.ExecuteAsync(Unpaid);
        var again = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        Assert.Same(details, again);
        Dispatcher.UIThread.RunJobs();
        Assert.Same(detailsView, window.GetVisualDescendants().OfType<OrderDetailsView>().Single(v => v.IsVisible));
        await WaitUntil(() => again.Order?.Id == Unpaid.Id);
        await again.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        await orders.ViewCommand.ExecuteAsync(PartlyPaid);
        details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.Order?.Id == PartlyPaid.Id);

        Assert.Equal(menu, shell.SelectedMenuIndex);
        Assert.Same(details, shell.SubPage);
        var texts = Texts(window);
        Assert.Contains("Order ORD-000007", texts);
        Assert.Contains("Complete blood count", texts);
        Assert.Contains("سكر صائم", texts);
        Assert.Contains("admin@test.local", texts);
        Assert.Contains("Partially paid", window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text));
        Assert.Equal(["200.00", "50.00", "150.00"], window.GetVisualDescendants().OfType<StatCard>().Select(c => c.Value));
        Assert.False(details.HasAddon);
        Assert.False(Named<StackPanel>(window, "AddonRow").IsVisible);

        await details.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(orders, shell.CurrentPage);
        Assert.Null(shell.SubPage);
        Assert.Equal(listReads, ListReads(app));
    }

    [AvaloniaFact]
    public async Task Details_show_the_addon_when_the_order_has_one()
    {
        using var app = Admin();
        var order = Details(PartlyPaid.Id) with { Total = 23000, Balance = 18000, Addon = 3000 };
        app.Api.OnGet($"{OrdersPath}/{PartlyPaid.Id}", order);
        var (shell, orders, window) = await ShowOrdersPageAsync(app);

        await orders.ViewCommand.ExecuteAsync(PartlyPaid);
        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasOrder);

        Assert.True(details.HasAddon);
        Assert.True(Named<StackPanel>(window, "AddonRow").IsVisible);
        Assert.Equal("30.00", Named<TextBlock>(window, "AddonText").Text);
        Assert.Contains("Our price", Texts(window));
        Assert.Equal(["230.00", "50.00", "180.00"], window.GetVisualDescendants().OfType<StatCard>().Select(c => c.Value));
    }

    [AvaloniaFact]
    public async Task New_order_button_shows_a_spinner_while_opening()
    {
        using var app = Admin();
        var (shell, orders, window) = await ShowOrdersPageAsync(app);
        var button = Named<Button>(window, "NewOrderButton");
        var spinner = Assert.Single(button.GetVisualDescendants().OfType<Mostawsef.Desktop.Components.Spinner>());
        var sawSpinner = false;
        orders.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OrdersViewModel.IsOpeningNewOrder) && orders.IsOpeningNewOrder)
            {
                Dispatcher.UIThread.RunJobs();
                sawSpinner = spinner.IsVisible;
            }
        };

        await orders.NewOrderCommand.ExecuteAsync(null);
        var page = Assert.IsType<NewOrderViewModel>(shell.CurrentPage);
        await WaitUntil(() => !page.IsPreparing);

        Assert.True(sawSpinner);
        Assert.False(orders.IsOpeningNewOrder);
        Assert.False(spinner.IsVisible);
    }

    [AvaloniaFact]
    public async Task Back_from_new_order_without_saving_keeps_the_list()
    {
        using var app = Admin();
        var (shell, orders, _) = await ShowOrdersPageAsync(app);
        var listReads = ListReads(app);

        await orders.NewOrderCommand.ExecuteAsync(null);
        Assert.IsType<NewOrderViewModel>(shell.CurrentPage);
        await app.Navigation.BackAsync();
        await WaitUntil(() => !orders.IsLoading);

        Assert.Same(orders, shell.CurrentPage);
        Assert.Equal(listReads, ListReads(app));
    }

    [AvaloniaFact]
    public async Task Saving_a_new_order_inserts_it_on_the_list_without_a_reload()
    {
        using var app = Admin();
        var created = Details(Guid.NewGuid());
        app.Api.On(HttpMethod.Post, OrdersPath, _ => FakeApiHandler.Json(created));
        var (shell, orders, _) = await ShowOrdersPageAsync(app);
        var listReads = ListReads(app);

        await orders.NewOrderCommand.ExecuteAsync(null);
        var page = Assert.IsType<NewOrderViewModel>(shell.CurrentPage);
        page.ChooseNewPatientCommand.Execute(null);
        page.PatientForm.FullName = "Omar";
        page.PatientForm.Phone = "0111";
        page.PatientForm.Gender = Genders.Male;
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.TestsStep && !page.IsCatalogLoading);
        page.AddTestCommand.Execute(page.Catalog.Single(t => t.Test.Id == Cbc.Id));
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.ReviewStep);
        await page.SaveCommand.ExecuteAsync(null);
        Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);

        await app.Navigation.BackAsync();
        await WaitUntil(() => !orders.IsLoading);

        Assert.Same(orders, shell.CurrentPage);
        Assert.Equal(listReads, ListReads(app));
        Assert.Equal(created.Id, orders.Items[0].Id);
        Assert.Equal(3, orders.TotalCount);
    }

    [AvaloniaFact]
    public async Task A_missing_order_shows_the_not_found_state()
    {
        using var app = Admin();
        app.Api.On(HttpMethod.Get, $"{OrdersPath}/{Unpaid.Id}", System.Net.HttpStatusCode.NotFound);
        var (shell, orders, window) = await ShowOrdersPageAsync(app);

        await orders.ViewCommand.ExecuteAsync(Unpaid);
        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasLoadError);

        Assert.Contains("This order no longer exists.", Texts(window));
    }

    [AvaloniaFact]
    public async Task A_failed_load_shows_the_error_state()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.FailWithNetworkError();

        var (_, orders, window) = await ShowOrdersPageAsync(app);

        Assert.True(orders.HasLoadError);
        Assert.Contains(window.GetVisualDescendants().OfType<ErrorState>(), e => e.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task In_arabic_the_list_is_translated_and_right_to_left()
    {
        using var app = Admin(AppLanguage.Arabic);

        var (_, _, window) = await ShowOrdersPageAsync(app);

        Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, window.FlowDirection);
        var texts = Texts(window);
        Assert.Contains("الطلبات", texts);
        Assert.Contains("طلب جديد", texts);
        Assert.Contains("مدفوع جزئياً", window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text));
    }

    private static int ListReads(TestServices app) =>
        app.Api.RequestsTo(HttpMethod.Get, OrdersPath).Count(r => r.RequestUri!.AbsolutePath == OrdersPath);
}

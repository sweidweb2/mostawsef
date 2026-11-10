using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Tests.Orders;

/// <summary>Sample data and the admin portal opened on the Orders page, shared by the order page tests.</summary>
internal static class OrderPages
{
    public const string OrdersPath = "/api/orders";
    public const string TestsPath = "/api/lab-tests";
    public const string PatientsPath = "/api/patients";

    public static readonly LabTestDto Cbc = new(Guid.NewGuid(), "CBC", "Complete blood count", 15000, 9000, true, DateTime.UtcNow, null, null, 0);

    public static readonly LabTestDto Sugar = new(Guid.NewGuid(), "GLU", "سكر صائم", 5000, 2000, true, DateTime.UtcNow, null, null, 0);

    public static readonly LabTestDto Retired = new(Guid.NewGuid(), "OLD", "Retired test", 1000, 500, false, DateTime.UtcNow, null, null, 0);

    public static readonly PatientDto Mona = new(
        Guid.NewGuid(), "Mona Adel", "010 1234 5678", null, null, false, Genders.Female, null, DateTime.UtcNow, null, null, 0);

    public static readonly PatientDto Omar = new(
        Guid.NewGuid(), "عمر خالد", "011 2222 3333", null, null, false, Genders.Male, null, DateTime.UtcNow, null, null, 0);

    public static readonly OrderListItemDto PartlyPaid = new(
        Guid.NewGuid(), "ORD-000007", DateTime.UtcNow, Mona.FullName, Mona.Phone, 2, 20000, 5000, OrderStatuses.PartiallyPaid);

    public static readonly OrderListItemDto Unpaid = new(
        Guid.NewGuid(), "ORD-000008", DateTime.UtcNow, "عمر خالد", "011 2222 3333", 1, 5000, 0, OrderStatuses.Pending);

    public static OrderDetailsDto Details(Guid id) => new(
        id, "ORD-000007", DateTime.UtcNow, "admin@test.local", Mona.Id, Mona.FullName, Mona.Phone, OrderStatuses.PartiallyPaid,
        Currencies.Egp, 20000, 5000, 15000,
        [new OrderItemDto(Cbc.Id, Cbc.Code, Cbc.Name, 15000), new OrderItemDto(Sugar.Id, Sugar.Code, Sugar.Name, 5000)]);

    /// <summary>Signed in as Admin; the server knows two orders and a catalog of two active tests and one inactive.</summary>
    public static TestServices Admin(AppLanguage language = AppLanguage.English)
    {
        var app = new TestServices(language);
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(OrdersPath, new PagedResult<OrderListItemDto>([PartlyPaid, Unpaid], 1, 20, 2));
        app.Api.On(HttpMethod.Get, TestsPath, request => CatalogResponse(request));
        app.Api.On(HttpMethod.Get, PatientsPath, request => PatientResponse(request));
        return app;
    }

    /// <summary>Mirrors the API: SearchText-style name/phone search for the new-order autocomplete.</summary>
    public static HttpResponseMessage PatientResponse(HttpRequestMessage request)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
        IEnumerable<PatientDto> patients = [Mona, Omar];
        var term = SearchText.Normalize(query["search"]);
        if (term.Length > 0)
        {
            patients = patients.Where(p =>
                SearchText.Normalize(p.FullName).Contains(term, StringComparison.Ordinal)
                || SearchText.Normalize(p.Phone).Contains(term, StringComparison.Ordinal)
                || SearchText.Normalize(p.GuardianPhone).Contains(term, StringComparison.Ordinal));
        }

        var items = patients.ToList();
        return FakeApiHandler.Json(new PagedResult<PatientDto>(items, 1, PageQuery.DefaultPageSize, items.Count));
    }

    /// <summary>Mirrors the API: optional active filter and SearchText-style name/code search.</summary>
    public static HttpResponseMessage CatalogResponse(HttpRequestMessage request)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
        IEnumerable<LabTestDto> tests = [Cbc, Sugar, Retired];
        if (bool.TryParse(query["active"], out var active))
        {
            tests = tests.Where(t => t.IsActive == active);
        }

        var term = SearchText.Normalize(query["search"]);
        if (term.Length > 0)
        {
            tests = tests.Where(t =>
                SearchText.Normalize(t.Name).Contains(term, StringComparison.Ordinal)
                || t.Code.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var items = tests.ToList();
        return FakeApiHandler.Json(new PagedResult<LabTestDto>(items, 1, PageQuery.MaxPageSize, items.Count));
    }

    /// <summary>The admin portal (with dialogs and toasts) showing the Orders page.</summary>
    public static async Task<(PortalShellViewModel Shell, OrdersViewModel Orders, Window Window)> ShowOrdersPageAsync(
        TestServices app, double width = 1280, double height = 800)
    {
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        var host = new DialogHost { Content = new ContentControl { Content = shell } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        window.Width = width;
        window.Height = height;
        app.Get<ToastService>().Attach(window);

        shell.SelectedMenuIndex = shell.MenuItems.ToList().FindIndex(p => p.PageType == typeof(OrdersViewModel));
        var orders = Assert.IsType<OrdersViewModel>(shell.CurrentPage);
        await WaitUntil(() => !orders.IsLoading);
        return (shell, orders, window);
    }

    /// <summary>New order opened from the Orders page on the patient step (catalog preloads while preparing).</summary>
    public static async Task<(PortalShellViewModel Shell, NewOrderViewModel Page, Window Window)> ShowNewOrderAsync(
        TestServices app, double width = 1280, double height = 800)
    {
        var (shell, orders, window) = await ShowOrdersPageAsync(app, width, height);
        await orders.NewOrderCommand.ExecuteAsync(null);
        var page = Assert.IsType<NewOrderViewModel>(shell.CurrentPage);
        await WaitUntil(() =>
            page.SelectedStep == NewOrderViewModel.PatientStep
            && !page.IsPreparing
            && !page.IsLoadingPatients
            && page.StepContent is not null);
        return (shell, page, window);
    }

    public static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Dispatcher.UIThread.RunJobs();
        Assert.True(condition());
    }

    public static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    public static List<string?> Toasts(Window window) =>
        window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message).ToList();
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Shell;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Patients;

[Collection(UiTestCollection.Name)]
public class PatientDetailsTests : UiTestBase
{
    private static async Task<(PortalShellViewModel Shell, PatientsViewModel Patients, Window Window)> ShowPatientsAsync(
        TestServices app, params OrderListItemDto[]? orders)
    {
        orders ??= [];
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(PatientsPath, new PagedResult<PatientDto>([Mona, Omar], 1, 40, 2));
        app.Api.OnGet(OrdersPath, new PagedResult<OrderListItemDto>(orders, 1, 100, orders.Length));
        app.Api.OnGet($"{OrdersPath}/{Unpaid.Id}", Details(Unpaid.Id));

        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        var window = app.Show(shell);
        shell.SelectedMenuIndex = shell.MenuItems.ToList().FindIndex(p => p.PageType == typeof(PatientsViewModel));
        var patients = Assert.IsType<PatientsViewModel>(shell.CurrentPage);
        await WaitUntil(() => !patients.IsLoading);
        Dispatcher.UIThread.RunJobs();
        return (shell, patients, window);
    }

    private static void Click(Window window, Control target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Clicking_a_patient_opens_their_details_with_their_orders_as_cards()
    {
        using var app = new TestServices();
        var (shell, _, window) = await ShowPatientsAsync(app, PartlyPaid, Unpaid);

        var row = window.GetVisualDescendants().OfType<DataGridRow>().First(r => r.DataContext is PatientDto { Id: var id } && id == Mona.Id);
        Click(window, row.GetVisualDescendants().OfType<DataGridCell>().ElementAt(2));

        var details = Assert.IsType<PatientDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.Orders.Count == 2);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Mona.Id, details.Patient!.Id);
        Assert.Contains($"patientId={Mona.Id}", app.Api.RequestsTo(HttpMethod.Get, OrdersPath).Last().RequestUri!.Query);
        var cards = window.GetVisualDescendants().OfType<Card>().Where(c => c.DataContext is OrderListItemDto).ToList();
        Assert.Equal([PartlyPaid.Number, Unpaid.Number], cards.Select(c => c.Title));
        Assert.Equal(Mona.FullName, Named<TextBlock>(window, "BreadcrumbSubPage").Text);
        Assert.Contains("Orders (2)", Texts(window));
    }

    [AvaloniaFact]
    public async Task Clicking_an_order_card_opens_that_order_and_the_breadcrumb_returns_to_the_patients()
    {
        using var app = new TestServices();
        var (shell, patients, window) = await ShowPatientsAsync(app, PartlyPaid, Unpaid);
        await patients.OpenCommand.ExecuteAsync(Mona);
        var details = Assert.IsType<PatientDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.Orders.Count == 2);
        Dispatcher.UIThread.RunJobs();

        var card = window.GetVisualDescendants().OfType<Card>().First(c => c.DataContext is OrderListItemDto { Id: var id } && id == Unpaid.Id);
        Click(window, card);

        var order = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => order.Order?.Id == Unpaid.Id);

        await shell.CloseSubPageAsync();
        Assert.Same(patients, shell.CurrentPage);
    }

    [AvaloniaFact]
    public async Task Patients_with_an_order_not_paid_in_full_are_tinted_light_yellow()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(PatientsPath, new PagedResult<PatientDto>([Mona with { HasUnpaidOrders = true }, Omar], 1, 40, 2));
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        var window = app.Show(shell);
        shell.SelectedMenuIndex = shell.MenuItems.ToList().FindIndex(p => p.PageType == typeof(PatientsViewModel));
        var patients = Assert.IsType<PatientsViewModel>(shell.CurrentPage);
        await WaitUntil(() => !patients.IsLoading);
        Dispatcher.UIThread.RunJobs();

        var rows = window.GetVisualDescendants().OfType<DataGridRow>().ToList();
        Assert.Equal(
            [(Mona.Id, true), (Omar.Id, false)],
            rows.Select(r => (((PatientDto)r.DataContext!).Id, r.Classes.Contains("unpaid"))));

        patients.ShowGridCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var cards = window.GetVisualDescendants().OfType<Card>().Where(c => c.DataContext is PatientDto).ToList();
        Assert.Equal(
            [(Mona.Id, true), (Omar.Id, false)],
            cards.Select(c => (((PatientDto)c.DataContext!).Id, c.Classes.Contains("unpaid"))));
    }

    [AvaloniaFact]
    public async Task A_patient_without_orders_shows_an_empty_message()
    {
        using var app = new TestServices();
        var (shell, patients, window) = await ShowPatientsAsync(app);

        await patients.OpenCommand.ExecuteAsync(Omar);
        var details = Assert.IsType<PatientDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => !details.IsLoading);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Omar.Id, details.Patient!.Id);
        Assert.Contains("This patient has no orders yet.", Texts(window));
    }
}

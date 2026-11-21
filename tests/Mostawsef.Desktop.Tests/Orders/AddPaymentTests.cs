using System.Net.Http;
using System.Net.Http.Json;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.Views.Admin;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Orders;

[Collection(UiTestCollection.Name)]
public class AddPaymentTests : UiTestBase
{
    /// <summary>The details page of <paramref name="order"/>, opened from the Orders list.</summary>
    private static async Task<(OrderDetailsViewModel Details, Window Window)> OpenDetailsAsync(
        TestServices app, OrderListItemDto listed, OrderDetailsDto order)
    {
        app.Api.OnGet($"{OrdersPath}/{listed.Id}", order);
        var (shell, orders, window) = await ShowOrdersPageAsync(app);
        await orders.ViewCommand.ExecuteAsync(listed);
        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasOrder);
        Dispatcher.UIThread.RunJobs();
        return (details, window);
    }

    private static AddPaymentFormViewModel OpenForm(OrderDetailsViewModel details, Window window)
    {
        _ = details.AddPaymentCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        return (AddPaymentFormViewModel)window.GetVisualDescendants().OfType<Modal>().Last().Content!;
    }

    [AvaloniaFact]
    public async Task The_add_payment_button_shows_only_while_money_is_due()
    {
        using var app = Admin();
        var (details, window) = await OpenDetailsAsync(app, PartlyPaid, Details(PartlyPaid.Id));

        Assert.True(details.CanAddPayment);
        Assert.True(Named<Button>(window, "AddPaymentButton").IsEffectivelyVisible);
        Assert.False(details.NeedsRefund);
    }

    [AvaloniaFact]
    public async Task A_fully_paid_order_has_no_add_payment_button()
    {
        using var app = Admin();
        var paid = Details(PartlyPaid.Id) with { Status = OrderStatuses.Paid, Paid = 25000, Balance = 0 };
        var (details, window) = await OpenDetailsAsync(app, PartlyPaid, paid);

        Assert.False(details.CanAddPayment);
        Assert.False(Named<Button>(window, "AddPaymentButton").IsEffectivelyVisible);
        Assert.False(Named<Badge>(window, "RefundBadge").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task A_payment_over_what_is_due_is_refused_without_calling_the_api()
    {
        using var app = Admin();
        var (details, window) = await OpenDetailsAsync(app, PartlyPaid, Details(PartlyPaid.Id));
        var form = OpenForm(details, window);

        Assert.Equal(200.00m, form.MaxAmount);
        form.Amount = 200.01m;
        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal(ErrorCodes.ExceedsBalance, form.Errors[nameof(form.Amount)]);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, $"{OrdersPath}/{PartlyPaid.Id}/payments"));
    }

    [AvaloniaFact]
    public async Task Saving_a_payment_updates_the_order_and_closes_the_form()
    {
        using var app = Admin();
        var (details, window) = await OpenDetailsAsync(app, PartlyPaid, Details(PartlyPaid.Id));
        AddPaymentRequest? sent = null;
        var settled = Details(PartlyPaid.Id) with
        {
            Status = OrderStatuses.Paid,
            Paid = 25000,
            Balance = 0,
            Payments =
            [
                .. Details(PartlyPaid.Id).Payments,
                new OrderPaymentDto(Guid.NewGuid(), DateTime.UtcNow, PaymentTypes.Payment, PaymentMethods.Card, 20000, null),
            ],
        };
        app.Api.On(HttpMethod.Post, $"{OrdersPath}/{PartlyPaid.Id}/payments", request =>
        {
            sent = request.Content!.ReadFromJsonAsync<AddPaymentRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(settled);
        });

        var form = OpenForm(details, window);
        form.Amount = 200m;
        form.Method = PaymentMethods.Card;
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new AddPaymentRequest { Amount = 20000, Method = PaymentMethods.Card }, sent);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.Equal(OrderStatuses.Paid, details.Order!.Status);
        Assert.Equal(2, details.Order.Payments.Count);
        Assert.False(details.CanAddPayment);
    }

    [AvaloniaFact]
    public async Task An_order_paid_beyond_its_total_shows_the_refund_due_badge()
    {
        using var app = Admin();
        var over = Details(PartlyPaid.Id) with { Status = OrderStatuses.Paid, Total = 18000, Paid = 20000, Balance = -2000 };
        var (details, window) = await OpenDetailsAsync(app, PartlyPaid, over);

        Assert.True(details.NeedsRefund);
        Assert.False(details.CanAddPayment);
        var badge = Named<Badge>(window, "RefundBadge");
        Assert.True(badge.IsEffectivelyVisible);
        Assert.Equal(BadgeKind.Info, badge.Kind);
        Assert.Equal("Needs a refund of 20.00", badge.Text);
    }

    [AvaloniaFact]
    public async Task Editing_a_payment_opens_the_form_and_saving_updates_the_order()
    {
        using var app = Admin();
        var (details, window) = await OpenDetailsAsync(app, PartlyPaid, Details(PartlyPaid.Id));
        UpdatePaymentRequest? sent = null;
        var payment = details.Order!.Payments.Single();
        var updated = Details(PartlyPaid.Id) with
        {
            Status = OrderStatuses.Paid,
            Paid = 25000,
            Balance = 0,
            Payments = [payment with { Amount = 25000, Method = PaymentMethods.Card, Notes = "Full" }],
        };
        app.Api.On(HttpMethod.Put, $"{OrdersPath}/{PartlyPaid.Id}/payments/{PaymentId}", request =>
        {
            sent = request.Content!.ReadFromJsonAsync<UpdatePaymentRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(updated);
        });

        _ = details.EditPaymentCommand.ExecuteAsync(payment);
        Dispatcher.UIThread.RunJobs();
        var form = (AddPaymentFormViewModel)window.GetVisualDescendants().OfType<Modal>().Last().Content!;
        Assert.True(form.IsEditing);
        Assert.Equal(250.00m, form.MaxAmount);
        Assert.Equal(50.00m, form.Amount);
        form.Amount = 250m;
        form.Method = PaymentMethods.Card;
        form.Notes = "Full";
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new UpdatePaymentRequest { Amount = 25000, Method = PaymentMethods.Card, Notes = "Full" }, sent);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.Equal(OrderStatuses.Paid, details.Order!.Status);
        Assert.False(details.CanAddPayment);
        Assert.Contains("Payment updated.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Deleting_a_payment_asks_first_then_updates_the_order()
    {
        using var app = Admin();
        var (details, window) = await OpenDetailsAsync(app, PartlyPaid, Details(PartlyPaid.Id));
        var payment = details.Order!.Payments.Single();
        var unpaid = Details(PartlyPaid.Id) with
        {
            Status = OrderStatuses.Pending,
            Paid = 0,
            Balance = 25000,
            Payments = [],
        };
        app.Api.On(HttpMethod.Delete, $"{OrdersPath}/{PartlyPaid.Id}/payments/{PaymentId}", _ => FakeApiHandler.Json(unpaid));

        var cancelled = details.DeletePaymentCommand.ExecuteAsync(payment);
        Dispatcher.UIThread.RunJobs();
        var confirm = window.GetVisualDescendants().OfType<ConfirmDialog>().Single();
        Assert.Contains(Texts(window), t => t == "Delete this payment?");
        confirm.Close(false);
        await cancelled;
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Delete, $"{OrdersPath}/{PartlyPaid.Id}/payments/{PaymentId}"));

        var delete = details.DeletePaymentCommand.ExecuteAsync(payment);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.Api.RequestsTo(HttpMethod.Delete, $"{OrdersPath}/{PartlyPaid.Id}/payments/{PaymentId}"));
        Assert.Equal(OrderStatuses.Pending, details.Order!.Status);
        Assert.Empty(details.Order.Payments);
        Assert.True(details.CanAddPayment);
        Assert.Contains("Payment deleted.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Orders_paid_beyond_their_total_are_tinted_light_blue_in_the_list()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var overpaid = Unpaid with { Id = Guid.NewGuid(), Number = "ORD-000009", Total = 5000, Paid = 7000, Status = OrderStatuses.Paid };
        app.Api.OnGet(OrdersPath, new PagedResult<OrderListItemDto>([PartlyPaid, Unpaid, overpaid], 1, 20, 3));
        var (_, _, window) = await ShowOrdersPageAsync(app);
        Dispatcher.UIThread.RunJobs();

        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var rows = window.GetVisualDescendants().OfType<DataGridRow>().ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(["unpaid", "unpaid", "refund"], rows.Select(r => r.Classes.Contains("refund") ? "refund" : r.Classes.Contains("unpaid") ? "unpaid" : "plain"));
    }

    [AvaloniaFact]
    public async Task Clicking_an_order_record_opens_its_details()
    {
        using var app = Admin();
        app.Api.OnGet($"{OrdersPath}/{PartlyPaid.Id}", Details(PartlyPaid.Id));
        var (shell, _, window) = await ShowOrdersPageAsync(app);
        Dispatcher.UIThread.RunJobs();

        var row = window.GetVisualDescendants().OfType<DataGridRow>().First();
        var cell = row.GetVisualDescendants().OfType<DataGridCell>().ElementAt(2);
        var point = cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, Avalonia.Input.MouseButton.Left);
        window.MouseUp(point, Avalonia.Input.MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.Order?.Id == PartlyPaid.Id);
    }

    [AvaloniaFact]
    public async Task Choosing_a_status_filters_the_orders_from_page_one_and_all_statuses_clears_it()
    {
        using var app = Admin();
        var (_, orders, window) = await ShowOrdersPageAsync(app);
        Dispatcher.UIThread.RunJobs();
        var filter = Named<ComboBox>(window, "StatusFilter");
        Assert.Equal(4, filter.ItemCount);
        Assert.Equal("All statuses", Texts(window).First(t => t == "All statuses"));

        orders.SelectedStatus = OrdersViewModel_Status(orders, OrderStatuses.PartiallyPaid);
        await WaitUntil(() => !orders.IsLoading);

        var last = app.Api.RequestsTo(HttpMethod.Get, OrdersPath).Last().RequestUri!.Query;
        Assert.Contains("status=PartiallyPaid", last);
        Assert.Contains("page=1", last);

        orders.SelectedStatus = OrdersViewModel_Status(orders, null);
        await WaitUntil(() => !orders.IsLoading);
        Assert.DoesNotContain("status=", app.Api.RequestsTo(HttpMethod.Get, OrdersPath).Last().RequestUri!.Query);
    }

    private static Desktop.ViewModels.Admin.OrderStatusOption OrdersViewModel_Status(OrdersViewModel _, string? value) =>
        OrdersViewModel.StatusOptions.Single(o => o.Value == value);
}

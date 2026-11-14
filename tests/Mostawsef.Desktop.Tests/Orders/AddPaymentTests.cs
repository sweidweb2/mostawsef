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
        var paid = Details(PartlyPaid.Id) with { Status = OrderStatuses.Paid, Paid = 20000, Balance = 0 };
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

        Assert.Equal(150.00m, form.MaxAmount);
        form.Amount = 150.01m;
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
            Paid = 20000,
            Balance = 0,
            Payments =
            [
                .. Details(PartlyPaid.Id).Payments,
                new OrderPaymentDto(DateTime.UtcNow, PaymentTypes.Payment, PaymentMethods.Card, 15000, null),
            ],
        };
        app.Api.On(HttpMethod.Post, $"{OrdersPath}/{PartlyPaid.Id}/payments", request =>
        {
            sent = request.Content!.ReadFromJsonAsync<AddPaymentRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(settled);
        });

        var form = OpenForm(details, window);
        form.Amount = 150m;
        form.Method = PaymentMethods.Card;
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new AddPaymentRequest { Amount = 15000, Method = PaymentMethods.Card }, sent);
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
}

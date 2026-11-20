using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Orders;

/// <summary>Arabic letter shaping can't be asserted here: open an exported invoice once to check it by eye.</summary>
[Collection(UiTestCollection.Name)]
public class InvoiceTests : UiTestBase
{
    private static string Pdf(byte[] bytes) => System.Text.Encoding.ASCII.GetString(bytes, 0, 4);

    [AvaloniaTheory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Arabic)]
    public void The_invoice_renders_a_pdf_in_both_languages(AppLanguage language)
    {
        using var app = new TestServices(language);
        app.Get<ClinicContext>().Update(new ClinicSettingsDto("عيادة النور / Al Nour", "12 Nile St", "0223456789", Currencies.Egp, 0, null, null));
        var print = app.Get<PrintService>();
        var order = Details(Guid.NewGuid()) with { Addon = 23000, Total = 23000, Balance = 18000 };

        var pdf = print.Render(new InvoiceDocument(print.CreateContext(), order));

        Assert.Equal("%PDF", Pdf(pdf));
        Assert.True(pdf.Length > 5_000, $"Only {pdf.Length} bytes");
    }

    [AvaloniaTheory]
    [InlineData(OrderStatuses.Pending, 0L, 20000L)]
    [InlineData(OrderStatuses.Paid, 20000L, 0L)]
    [InlineData(OrderStatuses.Paid, 22000L, -2000L)]
    public void The_invoice_renders_for_unpaid_settled_and_overpaid_orders(string status, long paid, long balance)
    {
        using var app = new TestServices();
        var print = app.Get<PrintService>();
        var order = Details(Guid.NewGuid()) with { Status = status, Paid = paid, Balance = balance, Payments = [] };

        Assert.Equal("%PDF", Pdf(print.Render(new InvoiceDocument(print.CreateContext(), order))));
    }

    [AvaloniaFact]
    public async Task Exporting_asks_where_to_save_and_saves_the_pdf_there()
    {
        using var app = Admin();
        var window = app.Show(new ContentControl());
        app.Get<ToastService>().Attach(window);
        var invoices = app.Get<InvoiceService>();
        var order = Details(Guid.NewGuid());
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "mine", "my-invoice.pdf");

        var path = await invoices.AskExportPathAsync(order.Number);
        await invoices.ExportAsync(order, path!);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Invoice-ORD-000007.pdf"], app.SaveSuggestions);
        Assert.Equal(app.PickedSavePath, path);
        Assert.Equal("%PDF", Pdf(File.ReadAllBytes(path!)));
        Assert.Empty(app.OpenedFiles);
        Assert.Contains($"Invoice saved: {path}", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Cancelling_the_save_dialog_exports_nothing()
    {
        using var app = Admin();
        app.Api.OnGet($"{OrdersPath}/{PartlyPaid.Id}", Details(PartlyPaid.Id));
        var (_, orders, _) = await ShowOrdersPageAsync(app);
        app.PickedSavePath = null;

        await orders.ExportInvoiceCommand.ExecuteAsync(PartlyPaid);

        Assert.Equal(["ORD-000007.pdf".Insert(0, "Invoice-")], app.SaveSuggestions);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, $"{OrdersPath}/{PartlyPaid.Id}"));
        Assert.False(Directory.Exists(app.DownloadsFolder) && Directory.GetFiles(app.DownloadsFolder).Length > 0);
    }

    [AvaloniaFact]
    public async Task Printing_opens_a_page_that_starts_the_print_window()
    {
        using var app = Admin();

        await app.Get<InvoiceService>().PrintAsync(Details(Guid.NewGuid()));

        var path = Assert.Single(app.OpenedFiles);
        Assert.StartsWith("invoice-ORD-000007-", Path.GetFileName(path));
        Assert.EndsWith(".html", path, StringComparison.Ordinal);
        var html = File.ReadAllText(path);
        Assert.Contains("window.print()", html);
        Assert.Contains("data:image/png;base64,", html);
        File.Delete(path);
    }

    [AvaloniaFact]
    public async Task The_orders_list_exports_and_prints_a_record_through_its_icon_buttons()
    {
        using var app = Admin();
        app.Api.OnGet($"{OrdersPath}/{PartlyPaid.Id}", Details(PartlyPaid.Id));
        var (_, orders, window) = await ShowOrdersPageAsync(app);
        Dispatcher.UIThread.RunJobs();

        var buttons = window.GetVisualDescendants().OfType<IconButton>().ToList();
        Assert.Equal(2, buttons.Count(b => b.Command == orders.PrintInvoiceCommand));
        Assert.Equal(2, buttons.Count(b => b.Command == orders.ExportInvoiceCommand));

        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "list.pdf");
        await orders.ExportInvoiceCommand.ExecuteAsync(PartlyPaid);
        await orders.PrintInvoiceCommand.ExecuteAsync(PartlyPaid);

        Assert.Equal("%PDF", Pdf(File.ReadAllBytes(app.PickedSavePath)));
        var printed = Assert.Single(app.OpenedFiles);
        File.Delete(printed);
    }

    [AvaloniaFact]
    public async Task The_order_details_page_has_print_and_export_invoice_buttons()
    {
        using var app = Admin();
        app.Api.OnGet($"{OrdersPath}/{PartlyPaid.Id}", Details(PartlyPaid.Id));
        var (shell, orders, window) = await ShowOrdersPageAsync(app);
        await orders.ViewCommand.ExecuteAsync(PartlyPaid);
        var details = Assert.IsType<Desktop.ViewModels.Admin.OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasOrder);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<Button>(window, "PrintInvoiceButton").IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "ExportInvoiceButton").IsEffectivelyVisible);

        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "details.pdf");
        await details.ExportInvoiceCommand.ExecuteAsync(null);
        Assert.True(File.Exists(app.PickedSavePath));
    }

    [AvaloniaFact]
    public async Task The_spinner_runs_while_an_invoice_is_being_prepared()
    {
        using var app = Admin();
        var release = new TaskCompletionSource();
        app.Api.On(HttpMethod.Get, $"{OrdersPath}/{PartlyPaid.Id}", async (_, _) =>
        {
            await release.Task;
            return FakeApiHandler.Json(Details(PartlyPaid.Id));
        });
        var (_, orders, window) = await ShowOrdersPageAsync(app);

        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "spinner.pdf");
        var running = orders.ExportInvoiceCommand.ExecuteAsync(PartlyPaid);
        Dispatcher.UIThread.RunJobs();
        Assert.True(orders.IsInvoiceBusy);
        Assert.True(orders.ShowLoading);
        Assert.Contains(window.GetVisualDescendants().OfType<LoadingOverlay>(), o => o.IsLoading);

        release.SetResult();
        await running;
        Dispatcher.UIThread.RunJobs();
        Assert.False(orders.IsInvoiceBusy);
        Assert.False(orders.ShowLoading);
    }
}

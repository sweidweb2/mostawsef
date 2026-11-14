using System;
using System.Threading.Tasks;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.Services.Printing;

/// <summary>
/// The two invoice actions of an order, used by the orders list and the order details page:
/// Print opens the browser's print window with the invoice; Export asks where to save the PDF
/// (<see cref="AskExportPathAsync"/>), then saves it there. Failures are reported and shown as a toast, so callers just await.
/// </summary>
public class InvoiceService(
    OrdersApi orders,
    PrintService print,
    IToastService toasts,
    LocalizationService localization,
    ErrorReporter errors,
    SavePathPicker pickSavePath)
{
    public async Task PrintAsync(Guid orderId)
    {
        if (await LoadAsync(orderId) is { } order)
        {
            await PrintAsync(order);
        }
    }

    public async Task PrintAsync(OrderDetailsDto order)
    {
        try
        {
            await print.PrintDialogAsync(new InvoiceDocument(await print.CreateContextAsync(), order), $"invoice-{order.Number}");
        }
        catch (Exception ex)
        {
            Fail(ex, "Printing an invoice");
        }
    }

    /// <summary>Shows the "Save as" dialog for the invoice of <paramref name="orderNumber"/>; null when the user cancels.</summary>
    public async Task<string?> AskExportPathAsync(string orderNumber)
    {
        try
        {
            return await pickSavePath($"Invoice-{orderNumber}.pdf", localization.Get("Invoice.SaveTitle"));
        }
        catch (Exception ex)
        {
            Fail(ex, "Choosing where to save an invoice");
            return null;
        }
    }

    public async Task ExportAsync(Guid orderId, string path)
    {
        if (await LoadAsync(orderId) is { } order)
        {
            await ExportAsync(order, path);
        }
    }

    public async Task ExportAsync(OrderDetailsDto order, string path)
    {
        try
        {
            await print.SaveAsync(new InvoiceDocument(await print.CreateContextAsync(), order), path);
            toasts.Success(localization.Format("Invoice.Exported", path));
        }
        catch (Exception ex)
        {
            Fail(ex, "Exporting an invoice");
        }
    }

    private async Task<OrderDetailsDto?> LoadAsync(Guid orderId)
    {
        var result = await orders.GetOrderAsync(orderId);
        if (result.IsSuccess)
        {
            return result.Value;
        }

        toasts.Error(localization.Get(result.Error == ApiError.Unreachable ? "Error.UnreachableTitle" : "Error.Title"));
        return null;
    }

    private void Fail(Exception ex, string context)
    {
        errors.Report(ex, context: context);
        toasts.Error(localization.Get("Print.Failed"));
    }
}

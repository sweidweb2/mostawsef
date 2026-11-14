using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Contracts.Orders;

namespace Mostawsef.Desktop.Views.Admin;

public partial class OrdersView : UserControl
{
    public OrdersView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Tints the rows that need attention: light yellow for orders that still owe money (pending or partly paid),
    /// light blue for orders paid beyond their total (a refund is due).
    /// </summary>
    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        var order = e.Row.DataContext as OrderListItemDto;
        SetClass(e.Row, "refund", order is not null && order.Paid > order.Total);
        SetClass(e.Row, "unpaid", order is { Status: OrderStatuses.Pending or OrderStatuses.PartiallyPaid });
    }

    /// <summary>A click on a record opens its details, like the eye button (which handles its own click).</summary>
    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source
            || source.FindAncestorOfType<Button>() is not null
            || source.FindAncestorOfType<DataGridRow>() is not { DataContext: OrderListItemDto order }
            || DataContext is not OrdersViewModel orders)
        {
            return;
        }

        if (orders.ViewCommand.CanExecute(order))
        {
            orders.ViewCommand.Execute(order);
        }
    }

    private static void SetClass(DataGridRow row, string name, bool on)
    {
        if (on)
        {
            if (!row.Classes.Contains(name))
            {
                row.Classes.Add(name);
            }
        }
        else
        {
            row.Classes.Remove(name);
        }
    }
}

using Avalonia.Controls;
using Mostawsef.Contracts.Orders;

namespace Mostawsef.Desktop.Views.Admin;

public partial class OrdersView : UserControl
{
    public OrdersView()
    {
        InitializeComponent();
    }

    /// <summary>Tints the rows of orders that still owe money (pending or partly paid).</summary>
    private void OnLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        var unpaid = e.Row.DataContext is OrderListItemDto { Status: OrderStatuses.Pending or OrderStatuses.PartiallyPaid };
        if (unpaid)
        {
            if (!e.Row.Classes.Contains("unpaid"))
            {
                e.Row.Classes.Add("unpaid");
            }
        }
        else
        {
            e.Row.Classes.Remove("unpaid");
        }
    }
}

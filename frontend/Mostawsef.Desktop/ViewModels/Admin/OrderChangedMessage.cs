using Mostawsef.Contracts.Orders;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Sent (weakly, via the messenger) when an order was created or got a payment, so the lists that show it can refresh.</summary>
public sealed record OrderChangedMessage(OrderDetailsDto Order)
{
    /// <summary>The order as a list row (the lists patch a shown row in place instead of fetching again).</summary>
    public OrderListItemDto ToListItem() => new(
        Order.Id, Order.Number, Order.CreatedAt, Order.PatientName, Order.PatientPhone, Order.Items.Count, Order.Total, Order.Paid, Order.Status);
}

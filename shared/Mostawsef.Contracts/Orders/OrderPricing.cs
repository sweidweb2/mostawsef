namespace Mostawsef.Contracts.Orders;

/// <summary>
/// Shared money layout for an order. New orders charge labs plus our price; rows created before
/// <c>Addon</c> kept the old patient total and only copied it into <c>Addon</c>, so labs must not be
/// added again on those invoices.
/// </summary>
public static class OrderPricing
{
    public static long LabsTotal(IEnumerable<long> labCosts)
    {
        long sum = 0;
        foreach (var cost in labCosts)
        {
            sum += cost;
        }

        return sum;
    }

    public static long LabsTotal(IEnumerable<OrderItemDto> items)
    {
        long sum = 0;
        foreach (var item in items)
        {
            sum += item.LabCost;
        }

        return sum;
    }

    /// <summary>What the patient owes: our price plus the snapshotted lab costs (new-model pricing).</summary>
    public static long PatientTotal(long addonMinor, long labsTotal) => addonMinor + labsTotal;

    /// <summary>True when <paramref name="total"/> is our price plus the snapshotted lab costs (post-Addon pricing).</summary>
    public static bool TotalIncludesLabPrices(long total, long addon, long labsTotal) =>
        total == addon + labsTotal;

    public static bool TotalIncludesLabPrices(OrderDetailsDto order) =>
        TotalIncludesLabPrices(order.Total, order.Addon, LabsTotal(order.Items));

    /// <summary>
    /// Show the Addon line on details/invoice only when it is a real markup on a lab-inclusive total
    /// (hidden for legacy rows where Addon only mirrors the old patient charge).
    /// </summary>
    public static bool ShowsAddon(OrderDetailsDto order) =>
        order.Addon > 0 && TotalIncludesLabPrices(order);
}

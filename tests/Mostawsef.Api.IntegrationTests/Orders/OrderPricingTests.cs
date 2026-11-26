using Mostawsef.Contracts.Orders;

namespace Mostawsef.Api.IntegrationTests.Orders;

public class OrderPricingTests
{
    [Fact]
    public void Patient_total_is_addon_plus_labs()
    {
        Assert.Equal(12_000, OrderPricing.PatientTotal(addonMinor: 2_000, labsTotal: 10_000));
        Assert.Equal(10_000, OrderPricing.LabsTotal([4_000L, 6_000L]));
        Assert.Equal(10_000, OrderPricing.LabsTotal([
            new OrderItemDto(Guid.NewGuid(), "A", "Alpha", 4_000),
            new OrderItemDto(Guid.NewGuid(), "B", "Beta", 6_000),
        ]));
    }

    [Fact]
    public void Legacy_addon_mirroring_total_hides_the_addon_breakdown()
    {
        var legacy = Details(total: 8_000, addon: 8_000, labCost: 5_000);
        var current = Details(total: 13_000, addon: 8_000, labCost: 5_000);

        Assert.False(OrderPricing.TotalIncludesLabPrices(legacy));
        Assert.False(OrderPricing.ShowsAddon(legacy));
        Assert.True(OrderPricing.TotalIncludesLabPrices(current));
        Assert.True(OrderPricing.ShowsAddon(current));
        Assert.False(OrderPricing.ShowsAddon(Details(total: 5_000, addon: 0, labCost: 5_000)));
    }

    private static OrderDetailsDto Details(long total, long addon, long labCost) =>
        new(
            Guid.NewGuid(),
            "ORD-1",
            DateTime.UtcNow,
            "admin",
            Guid.NewGuid(),
            "Patient",
            "010",
            OrderStatuses.Pending,
            "EGP",
            total,
            0,
            total,
            addon,
            [new OrderItemDto(Guid.NewGuid(), "T1", "Test", labCost)],
            [],
            Version: 0);
}

using Mostawsef.Contracts.LabTests;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Api.IntegrationTests.Orders;

/// <summary>Domain guards for order money: the Application layer also refuses these, but the entity must not allow them.</summary>
public class OrderMoneyInvariantTests
{
    [Fact]
    public void AddPayment_refuses_an_amount_above_the_balance()
    {
        var order = OpenOrder(totalLabs: 1000, addon: 0);
        Assert.Throws<InvalidOperationException>(() =>
            order.AddPayment(PaymentMethod.Cash, 1001, DateTime.UtcNow, null));
        Assert.Empty(order.Payments);
        Assert.Equal(0, order.Paid);
    }

    [Fact]
    public void AddPayment_refuses_a_non_positive_amount()
    {
        var order = OpenOrder(totalLabs: 1000, addon: 0);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            order.AddPayment(PaymentMethod.Cash, 0, DateTime.UtcNow, null));
    }

    [Fact]
    public void UpdatePayment_refuses_raising_a_row_past_the_balance()
    {
        var order = OpenOrder(totalLabs: 1000, addon: 0);
        var payment = order.AddPayment(PaymentMethod.Cash, 400, DateTime.UtcNow, null);
        Assert.Throws<InvalidOperationException>(() =>
            order.UpdatePayment(payment, PaymentMethod.Cash, 1001, null));
        Assert.Equal(400, order.Paid);
    }

    [Fact]
    public void SetCharges_refuses_a_negative_addon()
    {
        var order = new Order("ORD-1", Guid.NewGuid(), "P", "p", "010", "EGP");
        Assert.Throws<ArgumentOutOfRangeException>(() => order.SetCharges(-1));
    }

    private static Order OpenOrder(long totalLabs, long addon)
    {
        var order = new Order("ORD-1", Guid.NewGuid(), "Patient", "patient", "010", "EGP");
        order.AddItem(new LabTest("T1", "Test", "test", totalLabs));
        order.SetCharges(addon);
        return order;
    }
}

using Mostawsef.Contracts.Orders;

namespace Mostawsef.Api.IntegrationTests.Orders;

public class OrderPaymentMappingTests
{
    [Fact]
    public void First_payment_is_omitted_when_amount_is_empty()
    {
        Assert.Null(OrderPaymentMapping.ToFirstPayment(null, PaymentMethods.Cash, "  note  "));
    }

    [Fact]
    public void Payment_requests_share_amount_method_and_trimmed_notes()
    {
        var first = OrderPaymentMapping.ToFirstPayment(12.5m, PaymentMethods.Card, "  Visa  ")!;
        var add = OrderPaymentMapping.ToAdd(12.5m, PaymentMethods.Card, "  Visa  ");
        var update = OrderPaymentMapping.ToUpdate(12.5m, PaymentMethods.Card, "  Visa  ");

        Assert.Equal((1250L, PaymentMethods.Card, "Visa"), (first.Amount, first.Method, first.Notes));
        Assert.Equal((1250L, PaymentMethods.Card, "Visa"), (add.Amount, add.Method, add.Notes));
        Assert.Equal((1250L, PaymentMethods.Card, "Visa"), (update.Amount, update.Method, update.Notes));
        Assert.Null(OrderPaymentMapping.NormalizeNotes("   "));
        Assert.Null(OrderPaymentMapping.ToMinorAmount(null));
    }
}

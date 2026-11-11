using Mostawsef.Domain.Common;

namespace Mostawsef.Domain.Entities;

/// <summary>One test of an order, with the prices copied when it was ordered.</summary>
public class OrderItem : Entity
{
    private OrderItem()
    {
    }

    internal OrderItem(LabTest test)
    {
        LabTest = test;
        LabTestId = test.Id;
        LabPrice = test.LabCost;
        Price = test.Price;
    }

    public Guid OrderId { get; private set; }

    public Guid LabTestId { get; private set; }

    public LabTest? LabTest { get; private set; }

    /// <summary>What the lab charges the clinic (the test's LabCost at order time).</summary>
    public long LabPrice { get; private set; }

    /// <summary>The test's catalog price at order time (not editable per item).</summary>
    public long Price { get; private set; }
}

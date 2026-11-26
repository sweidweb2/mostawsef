using Mostawsef.Domain.Common;

namespace Mostawsef.Domain.Entities;

/// <summary>One test of an order, with the lab cost and labels copied when it was ordered.</summary>
public class OrderItem : Entity
{
    private OrderItem()
    {
    }

    internal OrderItem(LabTest test)
    {
        // Snapshot only: leave LabTest null so create can load catalog rows with AsNoTracking.
        LabTestId = test.Id;
        LabTestCode = test.Code;
        LabTestName = test.Name;
        LabPrice = test.LabCost;
    }

    public Guid OrderId { get; private set; }

    public Guid LabTestId { get; private set; }

    public LabTest? LabTest { get; private set; }

    /// <summary>Catalog code at order time (survives renames and soft-deletes).</summary>
    public string LabTestCode { get; private set; } = string.Empty;

    /// <summary>Catalog name at order time (survives renames and soft-deletes).</summary>
    public string LabTestName { get; private set; } = string.Empty;

    /// <summary>What the lab charges the clinic (the test's LabCost at order time).</summary>
    public long LabPrice { get; private set; }
}

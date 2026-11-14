using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Glyph inside a step circle: number, check, or missing warning.</summary>
public enum OrderStepStatus
{
    Number,
    Complete,
    Missing,
}

/// <summary>One step in the new-order indicator (bound from a single item template).</summary>
public sealed partial class OrderStepItem : ObservableObject
{
    public OrderStepItem(int index, string number, string titleKey, bool hasLineAfter)
    {
        Index = index;
        Number = number;
        TitleKey = titleKey;
        HasLineAfter = hasLineAfter;
    }

    public int Index { get; }

    public string Number { get; }

    public string TitleKey { get; }

    public bool HasLineAfter { get; }

    /// <summary>True when this is not the first step (draws the left half of the connector).</summary>
    public bool HasLineBefore => Index > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNumber), nameof(ShowCheck), nameof(ShowMissing))]
    public partial OrderStepStatus Status { get; private set; } = OrderStepStatus.Number;

    [ObservableProperty]
    public partial bool IsCurrent { get; private set; }

    /// <summary>Connector into this step (shared with the previous step's line-after).</summary>
    [ObservableProperty]
    public partial bool LineBeforeComplete { get; private set; }

    /// <summary>Connector out of this step toward the next.</summary>
    [ObservableProperty]
    public partial bool LineComplete { get; private set; }

    public bool ShowNumber => Status == OrderStepStatus.Number;

    public bool ShowCheck => Status == OrderStepStatus.Complete;

    public bool ShowMissing => Status == OrderStepStatus.Missing;

    public void Update(bool isCurrent, OrderStepStatus status, bool lineBeforeComplete, bool lineComplete)
    {
        IsCurrent = isCurrent;
        Status = status;
        LineBeforeComplete = lineBeforeComplete;
        LineComplete = lineComplete;
    }
}

/// <summary>ContentControl marker for the patient step (keeps only that visual tree alive).</summary>
public sealed class NewOrderPatientStep(NewOrderViewModel order)
{
    public NewOrderViewModel Order { get; } = order;
}

/// <summary>ContentControl marker for the tests step.</summary>
public sealed class NewOrderTestsStep(NewOrderViewModel order)
{
    public NewOrderViewModel Order { get; } = order;
}

/// <summary>ContentControl marker for the review step.</summary>
public sealed class NewOrderReviewStep(NewOrderViewModel order)
{
    public NewOrderViewModel Order { get; } = order;
}

/// <summary>A card of the catalog. Selected tests stay in the grid as a disabled ghost.</summary>
public partial class CatalogTestItem(LabTestDto test) : ObservableObject
{
    private readonly string _searchName = SearchText.Normalize(test.Name);

    public LabTestDto Test { get; } = test;

    /// <summary>Same rule as the API search: a normalized name or code containing the normalized term.</summary>
    public bool Matches(string normalizedTerm) =>
        _searchName.Contains(normalizedTerm, StringComparison.Ordinal)
        || Test.Code.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>A selected test; its catalog price contributes to the order total.</summary>
public partial class OrderItemDraft(CatalogTestItem item) : ObservableObject
{
    public CatalogTestItem Item { get; } = item;

    public LabTestDto Test => Item.Test;

    public FormErrors Errors { get; } = new();
}

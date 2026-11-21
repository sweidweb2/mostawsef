using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

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

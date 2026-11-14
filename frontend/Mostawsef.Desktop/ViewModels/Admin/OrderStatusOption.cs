namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>One choice of the orders status filter: <paramref name="Value"/> is the API status, null for "all".</summary>
public sealed record OrderStatusOption(string? Value, string LabelKey);

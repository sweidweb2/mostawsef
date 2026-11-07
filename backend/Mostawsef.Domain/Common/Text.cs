namespace Mostawsef.Domain.Common;

public static class Text
{
    /// <summary>Cuts a value to a column's maximum length so writes never fail on long input.</summary>
    public static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    public static string TruncateRequired(string? value, int maxLength) =>
        Truncate(value ?? string.Empty, maxLength)!;
}

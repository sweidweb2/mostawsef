namespace Mostawsef.Domain.Common;

public static class Text
{
    /// <summary>Separates the fields inside a search key, so a term cannot match across two fields.</summary>
    public const char SearchKeySeparator = '\n';

    /// <summary>Cuts a value to a column's maximum length so writes never fail on long input.</summary>
    public static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    public static string TruncateRequired(string? value, int maxLength) =>
        Truncate(value ?? string.Empty, maxLength)!;

    /// <summary>
    /// The value stored in a searchable "SearchText" column: each field normalized with <see cref="SearchText"/>, then joined.
    /// Search terms go through <see cref="NormalizeSearchTerm"/> so both sides compare the same way.
    /// </summary>
    public static string SearchKey(params string?[] fields) =>
        string.Join(SearchKeySeparator, fields.Select(SearchText.Normalize).Where(f => f.Length > 0));

    public static string NormalizeSearchTerm(string term) => SearchText.Normalize(term);
}

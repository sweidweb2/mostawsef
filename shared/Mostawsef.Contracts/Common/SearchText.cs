using System.Text;

namespace Mostawsef.Contracts.Common;

/// <summary>
/// Normalizes text for searching, so the way a name was typed does not matter. Store the normalized value in its own
/// column and normalize the search term the same way, then compare with Contains (works on PostgreSQL and SQLite).
/// The desktop filters local lists with the same rules.
/// Arabic: أ إ آ ٱ → ا, ة → ه, ى ی → ي, ؤ → و, ئ → ي, ک → ك, diacritics and tatweel removed, Arabic-Indic digits → 0-9.
/// Also lower-cases and collapses whitespace.
/// </summary>
public static class SearchText
{
    /// <summary>Separates the fields inside a search key, so a term cannot match across two fields.</summary>
    public const char KeySeparator = '\n';

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            var mapped = Map(c);
            if (mapped == '\0')
            {
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(mapped));
        }

        return builder.ToString();
    }

    /// <summary>The value of a multi-field search column (e.g. log SearchText): each field normalized, then joined.</summary>
    public static string Key(params string?[] fields) =>
        string.Join(KeySeparator, fields.Select(Normalize).Where(f => f.Length > 0));

    private static char Map(char c) => c switch
    {
        '\u0623' or '\u0625' or '\u0622' or '\u0671' => '\u0627', // أ إ آ ٱ → ا
        '\u0629' => '\u0647', // ة → ه
        '\u0649' or '\u06CC' or '\u0626' => '\u064A', // ى ی ئ → ي
        '\u0624' => '\u0648', // ؤ → و
        '\u06A9' => '\u0643', // ک → ك
        '\u0640' => '\0', // tatweel
        >= '\u064B' and <= '\u065F' or '\u0670' => '\0', // harakat, tanween, shadda, sukun, superscript alef
        >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')), // Arabic-Indic digits
        >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')), // Persian digits
        _ => c,
    };
}

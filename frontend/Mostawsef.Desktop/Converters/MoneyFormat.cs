using System.Globalization;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Converters;

/// <summary>
/// Minor units -> "1,250.50 EGP" / "1,250.50 ج.م". Digits and separators stay Latin in both languages
/// (like the rest of the app); only the currency name is translated ("Currency.&lt;code&gt;").
/// In views use Text="{l:Money Total}"; in code (e.g. printouts) call Format.
/// </summary>
public static class MoneyFormat
{
    private static readonly object Gate = new();
    private static LocalizationService? _cachedLocalization;
    private static int _cachedVersion;
    private static string? _cachedCode;
    private static string? _cachedSuffix;

    public static string Format(long minorUnits, string? currencyCode, LocalizationService localization)
    {
        var amount = Money.ToMajor(minorUnits).ToString("N2", CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(currencyCode))
        {
            return amount;
        }

        string suffix;
        lock (Gate)
        {
            if (!ReferenceEquals(_cachedLocalization, localization)
                || _cachedVersion != localization.Version
                || _cachedCode != currencyCode
                || _cachedSuffix is null)
            {
                _cachedLocalization = localization;
                _cachedVersion = localization.Version;
                _cachedCode = currencyCode;
                _cachedSuffix = " " + localization.Get(string.Concat("Currency.", currencyCode));
            }

            suffix = _cachedSuffix;
        }

        return string.Concat(amount, suffix);
    }
}

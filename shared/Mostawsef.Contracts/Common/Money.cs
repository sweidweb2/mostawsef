namespace Mostawsef.Contracts.Common;

/// <summary>
/// Amounts travel and are stored as whole minor units (piasters, cents): 1250.50 EGP is 125050.
/// Every supported currency has 100 minor units. Convert only at the edges (form input, display).
/// </summary>
public static class Money
{
    public const int MinorUnitsPerUnit = 100;

    public static long ToMinor(decimal amount) =>
        (long)Math.Round(amount * MinorUnitsPerUnit, MidpointRounding.AwayFromZero);

    public static decimal ToMajor(long minorUnits) => minorUnits / (decimal)MinorUnitsPerUnit;
}

/// <summary>ISO 4217 codes the clinic can choose. The desktop shows "Currency.&lt;code&gt;".</summary>
public static class Currencies
{
    public const string Egp = "EGP";
    public const string Usd = "USD";
    public const string Eur = "EUR";
    public const string Sar = "SAR";
    public const string Aed = "AED";

    public static readonly IReadOnlyList<string> All = [Egp, Usd, Eur, Sar, Aed];
}

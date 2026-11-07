using System.Globalization;

namespace Mostawsef.Domain.Common;

/// <summary>Readable document numbers: Format("P", 123) is "P-000123". Numbers past six digits are kept whole.</summary>
public static class DocumentNumber
{
    public const int Digits = 6;

    public static string Format(string prefix, long value) =>
        $"{prefix}-{value.ToString(new string('0', Digits), CultureInfo.InvariantCulture)}";
}

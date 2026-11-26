using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Mostawsef.Application.Common;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;

namespace Mostawsef.Application.Modules.LabTests;

/// <summary>One cell pair from a Name/Price sheet before validation.</summary>
internal readonly record struct LabTestImportRawRow(int RowNumber, string? Name, string? PriceText);

/// <summary>
/// Reads and writes the lab-test import CSV with BCL only: Name + Price ($0.08 style).
/// Accepts comma or semicolon separators. Stops after <see cref="LabTestRules.ImportEmptyStopRows"/>
/// empty rows past the last filled row.
/// </summary>
internal static class LabTestImportFile
{
    public static (byte[] Content, string FileName, string ContentType) BuildTemplate()
    {
        // BOM so Excel keeps Arabic names readable; sample row shows the $ price shape.
        var text = "Name,Price\r\nComplete blood count,$12.00\r\n";
        var bom = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(text);
        var bytes = new byte[bom.Length + body.Length];
        bom.CopyTo(bytes, 0);
        body.CopyTo(bytes, bom.Length);
        return (bytes, "lab-tests-import-template.csv", "text/csv");
    }

    public static List<LabTestImportRawRow> ReadRows(Stream stream, string? fileName)
    {
        if (stream.CanSeek && stream.Length > LabTestRules.MaxImportBytes)
        {
            throw AppException.Validation("File", ErrorCodes.TooLarge);
        }

        var extension = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
        if (extension is not ("csv" or ""))
        {
            throw AppException.Validation("File", ErrorCodes.Invalid);
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        if (buffer.Length == 0)
        {
            throw AppException.Validation("File", ErrorCodes.Required);
        }

        if (buffer.Length > LabTestRules.MaxImportBytes)
        {
            throw AppException.Validation("File", ErrorCodes.TooLarge);
        }

        buffer.Position = 0;
        return ReadCsv(buffer);
    }

    /// <summary>
    /// Parses a sheet price like "$0.08", "2.00", "$1,234.50", "12,50", or "1.234,50" into minor units.
    /// Arabic-Indic digits are accepted. On failure, <paramref name="errorCode"/> is <c>invalid</c> or <c>range|…</c>.
    /// </summary>
    public static bool TryParsePrice(
        string? text,
        out long minorUnits,
        [NotNullWhen(false)] out string? errorCode)
    {
        minorUnits = 0;
        errorCode = null;
        if (!TryParseMajor(text, out var major) || major < 0)
        {
            errorCode = ErrorCodes.Invalid;
            return false;
        }

        minorUnits = Money.ToMinor(major);
        if (minorUnits > LabTestRules.MaxAmount)
        {
            errorCode = $"range|0|{LabTestRules.MaxAmount}";
            return false;
        }

        return true;
    }

    private static bool TryParseMajor(string? text, out decimal major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Span<char> buffer = stackalloc char[text.Length];
        var length = 0;
        foreach (var c in text.AsSpan().Trim())
        {
            buffer[length++] = MapPriceChar(c);
        }

        var span = buffer[..length].Trim();
        while (span.Length > 0 && (span[0] is '$' or '€' or '£' or '¤'))
        {
            span = span[1..].TrimStart();
        }

        while (span.Length > 0 && (span[^1] is '$' or '€' or '£' or '¤'))
        {
            span = span[..^1].TrimEnd();
        }

        if (span.Length == 0)
        {
            return false;
        }

        var lastSep = -1;
        for (var i = span.Length - 1; i >= 0; i--)
        {
            if (span[i] is '.' or ',')
            {
                lastSep = i;
                break;
            }
        }

        Span<char> cleaned = stackalloc char[span.Length + 1];
        var n = 0;
        if (lastSep < 0)
        {
            if (!CopyDigitsAndSign(span, cleaned, ref n))
            {
                return false;
            }
        }
        else
        {
            var integer = span[..lastSep];
            var fractional = span[(lastSep + 1)..];
            if (fractional.Length == 0)
            {
                return false;
            }

            var sep = span[lastSep];
            var onlyOneSep = integer.IndexOfAny('.', ',') < 0;
            // "1,234" (comma + exactly 3 digits, non-zero integer) is thousands; "12,50" and "0,080" stay decimals.
            // A lone '.' is always a decimal ("0.080"), including three fraction digits.
            var thousandsOnly = onlyOneSep
                && sep == ','
                && fractional.Length == 3
                && !IntegerIsZero(integer);
            if (thousandsOnly)
            {
                if (!CopyDigitsAndSign(span, cleaned, ref n, skipSeparators: true))
                {
                    return false;
                }
            }
            else
            {
                if (fractional.Length > 6)
                {
                    return false;
                }

                if (integer.Length > 0)
                {
                    if (!CopyDigitsAndSign(integer, cleaned, ref n, skipSeparators: true))
                    {
                        return false;
                    }
                }
                else
                {
                    cleaned[n++] = '0';
                }

                cleaned[n++] = '.';
                foreach (var c in fractional)
                {
                    if (c is < '0' or > '9')
                    {
                        return false;
                    }

                    cleaned[n++] = c;
                }
            }
        }

        return decimal.TryParse(
            cleaned[..n],
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out major);
    }

    /// <summary>True only when both cells look like column titles (avoids dropping a first test named "Test").</summary>
    public static bool IsHeaderRow(string? name, string? price)
    {
        var n = (name ?? string.Empty).Trim();
        var p = (price ?? string.Empty).Trim();
        if (n.Length == 0 || p.Length == 0)
        {
            return false;
        }

        return LooksLikeNameHeader(n) && LooksLikePriceHeader(p);
    }

    private static bool LooksLikeNameHeader(string value) =>
        value.Equals("name", StringComparison.OrdinalIgnoreCase)
        || value.Equals("test name", StringComparison.OrdinalIgnoreCase)
        || value.Equals("test", StringComparison.OrdinalIgnoreCase)
        || value.Equals("اسم", StringComparison.OrdinalIgnoreCase)
        || value.Equals("اسم التحليل", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePriceHeader(string value) =>
        value.Equals("price", StringComparison.OrdinalIgnoreCase)
        || value.Equals("lab cost", StringComparison.OrdinalIgnoreCase)
        || value.Equals("cost", StringComparison.OrdinalIgnoreCase)
        || value.Equals("labcost", StringComparison.OrdinalIgnoreCase)
        || value.Equals("السعر", StringComparison.OrdinalIgnoreCase)
        || value.Equals("التكلفة", StringComparison.OrdinalIgnoreCase)
        || value.Equals("تكلفة المعمل", StringComparison.OrdinalIgnoreCase);

    private static List<LabTestImportRawRow> ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<LabTestImportRawRow>();
        var rowNumber = 0;
        var consecutiveEmpty = 0;
        var seenData = false;
        char? delimiter = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            rowNumber++;
            delimiter ??= DetectDelimiter(line);
            var (name, price) = ParseCsvLine(line, delimiter.Value);
            if (!seenData && IsHeaderRow(name, price))
            {
                continue;
            }

            if (IsBlank(name) && IsBlank(price))
            {
                if (!seenData)
                {
                    continue;
                }

                consecutiveEmpty++;
                if (consecutiveEmpty >= LabTestRules.ImportEmptyStopRows)
                {
                    break;
                }

                continue;
            }

            seenData = true;
            consecutiveEmpty = 0;
            rows.Add(new LabTestImportRawRow(rowNumber, name, price));
            if (rows.Count > LabTestRules.MaxImportRows)
            {
                throw AppException.Validation("File", ErrorCodes.TooLarge);
            }
        }

        return rows;
    }

    /// <summary>Prefer ';' when the line has more semicolons than commas (Excel locale CSV).</summary>
    private static char DetectDelimiter(string line)
    {
        var commas = 0;
        var semis = 0;
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (inQuotes)
            {
                continue;
            }

            switch (c)
            {
                case ',':
                    commas++;
                    break;
                case ';':
                    semis++;
                    break;
            }
        }

        return semis > commas ? ';' : ',';
    }

    private static (string? Name, string? Price) ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>(2);
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return (
            fields.Count > 0 ? fields[0].Trim() : null,
            fields.Count > 1 ? fields[1].Trim() : null);
    }

    private static bool IntegerIsZero(ReadOnlySpan<char> integer)
    {
        var sawDigit = false;
        foreach (var c in integer)
        {
            if (c is '.' or ',' or ' ' or '+' or '-')
            {
                continue;
            }

            if (c is < '0' or > '9' || c != '0')
            {
                return false;
            }

            sawDigit = true;
        }

        return sawDigit;
    }

    private static char MapPriceChar(char c) => c switch
    {
        >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')),
        >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')),
        '\u00A0' => ' ',
        _ => c,
    };

    private static bool CopyDigitsAndSign(ReadOnlySpan<char> span, Span<char> destination, ref int n, bool skipSeparators = false)
    {
        var started = false;
        foreach (var c in span)
        {
            if (!started && c is ' ' or '\t')
            {
                continue;
            }

            if (!started && c is '+' or '-')
            {
                destination[n++] = c;
                started = true;
                continue;
            }

            if (c is ' ' or '\t')
            {
                continue;
            }

            if (skipSeparators && c is '.' or ',')
            {
                continue;
            }

            if (c is < '0' or > '9')
            {
                return false;
            }

            destination[n++] = c;
            started = true;
        }

        return n > 0 && destination[n - 1] is >= '0' and <= '9';
    }

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}

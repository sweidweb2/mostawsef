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
/// Stops after <see cref="LabTestRules.ImportEmptyStopRows"/> empty rows past the last filled row.
/// </summary>
internal static class LabTestImportFile
{
    private const int MaxFileBytes = 2 * 1024 * 1024;

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
        if (stream.CanSeek && stream.Length > MaxFileBytes)
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

        if (buffer.Length > MaxFileBytes)
        {
            throw AppException.Validation("File", ErrorCodes.TooLarge);
        }

        buffer.Position = 0;
        return ReadCsv(buffer);
    }

    /// <summary>Parses a sheet price like "$0.08", "2.00", or "$1,234.50" into minor units.</summary>
    public static bool TryParsePrice(string? text, out long minorUnits)
    {
        minorUnits = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.AsSpan().Trim();
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

        // Drop thousands separators; keep a single decimal point (US / template style).
        Span<char> cleaned = stackalloc char[span.Length];
        var n = 0;
        foreach (var c in span)
        {
            if (c is ',' or ' ' or '\u00A0')
            {
                continue;
            }

            cleaned[n++] = c;
        }

        if (!decimal.TryParse(cleaned[..n], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var major)
            || major < 0)
        {
            return false;
        }

        minorUnits = Money.ToMinor(major);
        return minorUnits <= LabTestRules.MaxAmount;
    }

    public static bool IsHeaderRow(string? name, string? price)
    {
        var n = (name ?? string.Empty).Trim();
        var p = (price ?? string.Empty).Trim();
        if (n.Length == 0 && p.Length == 0)
        {
            return false;
        }

        return LooksLikeNameHeader(n) || LooksLikePriceHeader(p);
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
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            rowNumber++;
            var (name, price) = ParseCsvLine(line);
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

    private static (string? Name, string? Price) ParseCsvLine(string line)
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

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        fields.Add(current.ToString());
        return (
            fields.Count > 0 ? fields[0].Trim() : null,
            fields.Count > 1 ? fields[1].Trim() : null);
    }

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}

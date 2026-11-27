using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Contracts.LabTests;

/// <summary>Limits shared by the API and the desktop forms. Amounts are minor units of the clinic currency.</summary>
public static class LabTestRules
{
    public const int CodeMaxLength = 20;
    public const int NameMaxLength = 150;

    /// <summary>1,000,000.00 in the clinic currency.</summary>
    public const long MaxAmount = 100_000_000;

    /// <summary>Hard cap on non-empty import rows so a huge sheet cannot stall the API.</summary>
    public const int MaxImportRows = 500;

    /// <summary>Largest import upload the API and parser accept (bytes).</summary>
    public const int MaxImportBytes = 2 * 1024 * 1024;

    /// <summary>After the last filled row, this many empty rows in a row ends the file.</summary>
    public const int ImportEmptyStopRows = 10;
}

/// <summary>CSV the Admin portal writes locally for import (Name + $ price; codes are generated on upload).</summary>
public static class LabTestImportCsv
{
    public const string FileName = "lab-tests-import-template.csv";

    /// <summary>Body without a BOM; writers should prepend UTF-8 BOM so Excel keeps Arabic names.</summary>
    public const string Content = "Name,Price\r\nComplete blood count,$12.00\r\n";
}

public record LabTestDto(
    Guid Id,
    string Code,
    string Name,
    long LabCost,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    int Version);

public record CreateLabTestRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(LabTestRules.CodeMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string Code { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(LabTestRules.NameMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [Range(0, LabTestRules.MaxAmount, ErrorMessage = ErrorCodes.Range)]
    public long? LabCost { get; init; }
}

/// <summary>Version is the value from LabTestDto; a stale one returns 409 concurrency_conflict.</summary>
public record UpdateLabTestRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(LabTestRules.CodeMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string Code { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(LabTestRules.NameMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [Range(0, LabTestRules.MaxAmount, ErrorMessage = ErrorCodes.Range)]
    public long? LabCost { get; init; }

    public bool IsActive { get; init; } = true;

    public int Version { get; init; }
}

/// <summary>Result of posting a Name/Price CSV or Excel file to the lab-tests import endpoint.</summary>
/// <param name="Imported">New tests created.</param>
/// <param name="Updated">Existing tests whose price (and display name) were overwritten by a matching name.</param>
/// <param name="Skipped">Bad rows and duplicate names later in the same file.</param>
public record ImportLabTestsResult(int Imported, int Updated, int Skipped);

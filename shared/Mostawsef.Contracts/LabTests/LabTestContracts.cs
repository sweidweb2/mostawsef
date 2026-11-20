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

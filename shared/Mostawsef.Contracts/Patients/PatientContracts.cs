using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Contracts.Patients;

/// <summary>Limits shared by the API and the desktop forms.</summary>
public static class PatientRules
{
    public const int FullNameMaxLength = 100;
    public const int PhoneMaxLength = 30;
    public const int NotesMaxLength = 2000;

    /// <summary>Digits (Latin or Arabic), spaces, + - ( ). Surrounding spaces are trimmed by the API.</summary>
    public const string PhonePattern = @"^[\d\s()+-]*$";

    /// <summary>Earliest accepted date of birth; the latest is today (checked by the API).</summary>
    public static readonly DateOnly MinDateOfBirth = new(1900, 1, 1);
}

/// <summary>Gender values. The desktop shows "Gender.&lt;value&gt;".</summary>
public static class Genders
{
    public const string Male = "Male";
    public const string Female = "Female";

    public static readonly IReadOnlyList<string> All = [Male, Female];
}

public record PatientDto(
    Guid Id,
    string FullName,
    string Phone,
    string? GuardianPhone,
    string? Notes,
    bool IsInsured,
    string Gender,
    DateOnly? DateOfBirth,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    int Version);

public record CreatePatientRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(PatientRules.FullNameMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string FullName { get; init; } = string.Empty;

    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(PatientRules.PhoneMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    [RegularExpression(PatientRules.PhonePattern, ErrorMessage = ErrorCodes.Invalid)]
    public string Phone { get; init; } = string.Empty;

    [MaxLength(PatientRules.PhoneMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    [RegularExpression(PatientRules.PhonePattern, ErrorMessage = ErrorCodes.Invalid)]
    public string? GuardianPhone { get; init; }

    [MaxLength(PatientRules.NotesMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string? Notes { get; init; }

    public bool IsInsured { get; init; }

    [Required(ErrorMessage = ErrorCodes.Required)]
    [AllowedValues(Genders.Male, Genders.Female, ErrorMessage = ErrorCodes.Invalid)]
    public string Gender { get; init; } = string.Empty;

    /// <summary>Optional; between <see cref="PatientRules.MinDateOfBirth"/> and today, else 400 "invalid".</summary>
    public DateOnly? DateOfBirth { get; init; }
}

/// <summary>Version is the value from PatientDto; a stale one returns 409 concurrency_conflict.</summary>
public record UpdatePatientRequest : CreatePatientRequest
{
    public int Version { get; init; }
}

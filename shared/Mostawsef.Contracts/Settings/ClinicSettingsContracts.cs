using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Contracts.Settings;

public static class ClinicSettingsRules
{
    public const int NameMaxLength = 150;
    public const int AddressMaxLength = 300;
    public const int PhoneMaxLength = 30;
}

public record ClinicSettingsDto(
    string Name,
    string? Address,
    string? Phone,
    string CurrencyCode,
    int Version,
    DateTime? UpdatedAt,
    string? UpdatedBy);

/// <summary>Version is the value from ClinicSettingsDto; a stale one returns 409 concurrency_conflict.</summary>
public record UpdateClinicSettingsRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [MaxLength(ClinicSettingsRules.NameMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(ClinicSettingsRules.AddressMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string? Address { get; init; }

    [MaxLength(ClinicSettingsRules.PhoneMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string? Phone { get; init; }

    [Required(ErrorMessage = ErrorCodes.Required)]
    [AllowedValues(Currencies.Egp, Currencies.Usd, Currencies.Eur, Currencies.Sar, Currencies.Aed, ErrorMessage = ErrorCodes.Invalid)]
    public string CurrencyCode { get; init; } = Currencies.Egp;

    public int Version { get; init; }
}

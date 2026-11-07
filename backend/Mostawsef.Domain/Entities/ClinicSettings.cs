using Mostawsef.Domain.Common;

namespace Mostawsef.Domain.Entities;

/// <summary>The clinic's own details for headers and printouts. Exactly one row, created at startup.</summary>
public class ClinicSettings : Entity
{
    public const int NameMaxLength = 150;
    public const int AddressMaxLength = 300;
    public const int PhoneMaxLength = 30;
    public const int CurrencyCodeLength = 3;

    private ClinicSettings()
    {
    }

    public ClinicSettings(string name, string currencyCode) => Update(name, null, null, currencyCode);

    public string Name { get; private set; } = string.Empty;

    public string? Address { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>ISO 4217 code, e.g. "EGP". Amounts are stored in its minor units.</summary>
    public string CurrencyCode { get; private set; } = string.Empty;

    public void Update(string name, string? address, string? phone, string currencyCode)
    {
        Name = name.Trim();
        Address = Blank(address);
        Phone = Blank(phone);
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

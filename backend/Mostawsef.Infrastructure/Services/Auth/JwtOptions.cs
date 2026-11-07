namespace Mostawsef.Infrastructure.Services.Auth;

/// <summary>Bound from the "Jwt" configuration section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Mostawsef.Api";

    public string Audience { get; set; } = "Mostawsef.Desktop";

    /// <summary>HMAC-SHA256 signing key, at least 32 characters. Must be unique per installation.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 480;
}

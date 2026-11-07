using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Mostawsef.Infrastructure.Services.Auth;

/// <summary>Bound from the "Jwt" configuration section and validated when the API starts.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "Mostawsef.Api";

    [Required]
    public string Audience { get; set; } = "Mostawsef.Desktop";

    /// <summary>HMAC-SHA256 signing key, at least 32 characters. Must be unique per installation.</summary>
    [Required(ErrorMessage = "Jwt:Key must be configured.")]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters long.")]
    public string Key { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpiryMinutes { get; set; } = 480;

    /// <summary>
    /// The key that signs tokens (JwtTokenService) and validates them (JWT bearer authentication).
    /// A method, not a property: options validation reads every property and an empty Key would throw here.
    /// </summary>
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

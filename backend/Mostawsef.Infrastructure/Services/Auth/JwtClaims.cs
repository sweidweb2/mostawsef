namespace Mostawsef.Infrastructure.Services.Auth;

/// <summary>Claim names inside issued tokens (inbound claim mapping is disabled, so these are used as-is).</summary>
public static class JwtClaims
{
    public const string Role = "role";

    /// <summary>Matches <c>User.TokenVersion</c>; bumped when the password changes.</summary>
    public const string TokenVersion = "tv";
}

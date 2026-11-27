namespace Mostawsef.Infrastructure.Services.Auth;

/// <summary>Claim names inside issued tokens (inbound claim mapping is disabled, so these are used as-is).</summary>
public static class JwtClaims
{
    public const string Role = "role";

    /// <summary>Matches <c>User.TokenVersion</c>; bumped when the password changes.</summary>
    public const string TokenVersion = "tv";

    /// <summary>Present ("1") while <c>User.MustChangePassword</c>: the Super policy refuses such tokens (403).</summary>
    public const string MustChangePassword = "pwd_change";
}

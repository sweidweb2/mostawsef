namespace Mostawsef.Contracts.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

public record UserDto(Guid Id, string Username, string Role);

/// <summary>Role names shared by the API (authorization) and the desktop app (portal routing).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Super = "Super";
}

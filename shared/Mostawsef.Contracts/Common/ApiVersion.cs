namespace Mostawsef.Contracts.Common;

/// <summary>
/// Compiled into both the API and the desktop app. Increase it whenever a change would break an older desktop app
/// (a removed or renamed endpoint or field): the desktop compares it with the server's and warns on a mismatch.
/// </summary>
public static class ApiVersion
{
    public const int Current = 9;
}

/// <summary>GET /api/health.</summary>
public record HealthDto(string Status, string Database, int ApiVersion);

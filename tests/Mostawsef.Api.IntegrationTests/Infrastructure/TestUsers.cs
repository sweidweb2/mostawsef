using Mostawsef.Contracts.Auth;

namespace Mostawsef.Api.IntegrationTests.Infrastructure;

public record TestUser(string Username, string Password, string Role);

/// <summary>Seeded into the test database at API startup (Super via Seed:Users; clinic via DatabaseInitializer).</summary>
public static class TestUsers
{
    /// <summary>Built-in clinic Admin identity — open a session with clinic-session, not password login.</summary>
    public static readonly TestUser Admin = new(ClinicIdentity.Username, "", Roles.Admin);

    public static readonly TestUser Super = new("super@test.local", "Super#Pass1", Roles.Super);

    /// <summary>Password-seeded accounts only (clinic is created by the API initializer).</summary>
    public static readonly TestUser[] SeededLogins = [Super];

    /// <summary>
    /// Seeded the way a clinic install seeds Super: its password is an initial one that must be changed at first
    /// sign-in. Not in <see cref="SeededLogins"/>; only the initial-password tests use it.
    /// </summary>
    public static readonly TestUser InitialSuper = new("initial-super@test.local", "Initial#Pass1", Roles.Super);
}

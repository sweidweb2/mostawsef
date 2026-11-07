namespace Mostawsef.Api.IntegrationTests.Infrastructure;

public record TestUser(string Username, string Password, string Role);

/// <summary>Seeded into the test database at API startup.</summary>
public static class TestUsers
{
    public static readonly TestUser Admin = new("admin@test.local", "Admin#Pass1", "Admin");
    public static readonly TestUser Super = new("super@test.local", "Super#Pass1", "Super");

    public static readonly TestUser[] All = [Admin, Super];
}

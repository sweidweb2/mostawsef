using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Users;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class PasswordStorageTests(ApiFactory factory)
{
    [Fact]
    public async Task Passwords_are_stored_as_salted_pbkdf2_hashes()
    {
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var users = await db.Users.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);

        foreach (var testUser in TestUsers.All)
        {
            var stored = Assert.Single(users, u => u.Username == testUser.Username);

            Assert.NotEqual(testUser.Password, stored.PasswordHash);
            Assert.DoesNotContain(testUser.Password, stored.PasswordHash);
            Assert.StartsWith("AQAAAAI", stored.PasswordHash); // ASP.NET Identity v3 format (PBKDF2)
            Assert.Equal(PasswordCheck.Success, hasher.Verify(stored.PasswordHash, testUser.Password));
        }

        // Same password hashed twice gives different hashes (random salt).
        Assert.NotEqual(hasher.Hash("same"), hasher.Hash("same"));
    }

    [Fact]
    public async Task Passwords_set_by_user_management_are_hashed_and_never_returned()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, login) = await super.CreateUserAsync();
        var reset = await super.PostAsJsonAsync(
            $"api/users/{user.Id}/reset-password", new ResetPasswordRequest { NewPassword = "Reset#Pass22" }, TestContext.Current.CancellationToken);
        var details = await super.GetAsync($"api/users/{user.Id}", TestContext.Current.CancellationToken);
        var body = await details.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.DoesNotContain(login.Password, body);
        Assert.DoesNotContain("Reset#Pass22", body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.StartsWith("AQAAAAI", stored.PasswordHash);
        Assert.Equal(
            PasswordCheck.Success,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Verify(stored.PasswordHash, "Reset#Pass22"));
    }
}

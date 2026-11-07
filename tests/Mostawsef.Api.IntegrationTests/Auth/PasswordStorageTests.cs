using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
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

        Assert.Equal(TestUsers.All.Length, users.Count);
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
}

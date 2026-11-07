using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class PasswordRehashTests(ApiFactory factory)
{
    [Fact]
    public async Task Signing_in_with_an_outdated_hash_upgrades_it_and_records_the_login()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, login) = await super.CreateUserAsync();
        var oldHasher = new PasswordHasher<object>(Options.Create(new PasswordHasherOptions { IterationCount = 10_000 }));
        var oldHash = oldHasher.HashPassword(new object(), login.Password);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await db.Users.Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, oldHash), TestContext.Current.CancellationToken);
        Assert.Equal(PasswordCheck.SuccessRehashNeeded, hasher.Verify(oldHash, login.Password));

        var response = await factory.CreateClient().LoginAsync(login.Username, login.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.NotEqual(oldHash, stored.PasswordHash);
        Assert.Equal(PasswordCheck.Success, hasher.Verify(stored.PasswordHash, login.Password));
        Assert.NotNull(stored.LastLoginAt);
        Assert.InRange(stored.LastLoginAt.Value, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Signing_in_with_a_current_hash_keeps_it_and_records_the_login()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, login) = await super.CreateUserAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hashBefore = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken)).PasswordHash;

        var response = await factory.CreateClient().LoginAsync(login.Username, login.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
        Assert.Equal(hashBefore, stored.PasswordHash);
        Assert.NotNull(stored.LastLoginAt);
    }
}

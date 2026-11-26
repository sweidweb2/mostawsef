using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Auth;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class ClinicIdentityRepairTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Startup_repairs_a_soft_deleted_clinic_identity()
    {
        factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clinic = await db.Users.SingleAsync(u => u.Username == ClinicIdentity.Username, Ct);
            db.Users.Remove(clinic);
            await db.SaveChangesAsync(Ct);

            Assert.True(await db.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.Username == ClinicIdentity.Username && u.IsDeleted, Ct));
        }

        await factory.Services.InitializeDatabaseAsync([], Ct);
        factory.InvalidateSeededAccessTokens();

        var login = await factory.CreateClient().OpenClinicSessionAsync();
        Assert.Equal(ClinicIdentity.Username, login.User.Username);
        Assert.Equal(Roles.Admin, login.User.Role);
    }

    [Fact]
    public async Task Startup_repairs_a_wrong_role_clinic_username()
    {
        factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var clinic = await db.Users.SingleAsync(u => u.Username == ClinicIdentity.Username, Ct);
            clinic.ChangeRole(UserRole.Super);
            clinic.Deactivate();
            await db.SaveChangesAsync(Ct);
        }

        await factory.Services.InitializeDatabaseAsync([], Ct);
        factory.InvalidateSeededAccessTokens();

        var login = await factory.CreateClient().OpenClinicSessionAsync();
        Assert.Equal(Roles.Admin, login.User.Role);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var clinic = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
                .SingleAsync(u => u.Username == ClinicIdentity.Username, Ct);
            Assert.True(clinic.IsActive);
            Assert.Equal(UserRole.Admin, clinic.Role);
        }
    }

    [Fact]
    public async Task Startup_deactivates_legacy_non_clinic_admin_accounts()
    {
        factory.CreateClient();
        Guid legacyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var legacy = new User(
                $"legacy-admin-{Guid.NewGuid():N}"[..32],
                "Legacy Admin",
                "legacy admin",
                "not-a-real-hash",
                UserRole.Admin);
            db.Users.Add(legacy);
            await db.SaveChangesAsync(Ct);
            legacyId = legacy.Id;
        }

        await factory.Services.InitializeDatabaseAsync([], Ct);
        factory.InvalidateSeededAccessTokens();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var legacy = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
                .IgnoreQueryFilters()
                .SingleAsync(u => u.Id == legacyId, Ct);
            Assert.False(legacy.IsActive);
            Assert.Equal(UserRole.Admin, legacy.Role);
        }

        // Built-in clinic entry still works.
        var login = await factory.CreateClient().OpenClinicSessionAsync();
        Assert.Equal(ClinicIdentity.Username, login.User.Username);
    }
}

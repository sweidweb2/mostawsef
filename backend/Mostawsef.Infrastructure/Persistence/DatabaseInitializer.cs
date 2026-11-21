using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>Bound from "Seed:Users". Users are created only if their username was never used (deleted users count).</summary>
public class SeedUser
{
    public string Username { get; set; } = string.Empty;

    /// <summary>Defaults to the username.</summary>
    public string? FullName { get; set; }

    public string Password { get; set; } = string.Empty;

    public UserRole Role { get; set; }
}

public static class DatabaseInitializer
{
    /// <summary>Applies pending migrations (and their data backfills), then creates any missing seed users (passwords are hashed).</summary>
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        IEnumerable<SeedUser> seedUsers,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        if (pending.Contains(SearchTextBackfill.Migration))
        {
            var rebuilt = await SearchTextBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled the search text of {Count} existing log rows", rebuilt);
        }

        if (pending.Contains(OrderOurPriceBackfill.Migration))
        {
            var priced = await OrderOurPriceBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled Total on {Count} existing orders", priced);
        }

        if (pending.Contains(OrderSearchTextBackfill.Migration))
        {
            var searchable = await OrderSearchTextBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled SearchText on {Count} existing orders", searchable);
        }

        if (pending.Contains(PatientSearchTextBackfill.Migration))
        {
            var patients = await PatientSearchTextBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled SearchText on {Count} existing patients", patients);
        }

        if (pending.Contains(PatientSearchTextRebuild.Migration)
            || await PatientSearchTextRebuild.ShouldRunAsync(db, cancellationToken))
        {
            var rebuilt = await PatientSearchTextRebuild.RunAsync(db, cancellationToken);
            logger.LogInformation("Rebuilt SearchText on {Count} patients after dropping guardian phone", rebuilt);
        }

        if (pending.Contains(OrderListAggregatesBackfill.Migration))
        {
            var aggregates = await OrderListAggregatesBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled TestsCount and Paid on {Count} existing orders", aggregates);
        }

        if (pending.Contains(OrderItemSnapshotBackfill.Migration))
        {
            var snapshots = await OrderItemSnapshotBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Filled lab-test snapshots on {Count} existing order items", snapshots);
        }

        if (pending.Contains(OrderAddonBackfill.Migration)
            || await OrderAddonBackfill.ShouldRunAsync(db, cancellationToken))
        {
            var addons = await OrderAddonBackfill.RunAsync(db, cancellationToken);
            logger.LogInformation("Copied the saved total into Addon on {Count} existing orders", addons);
        }

        if (!await db.Set<ClinicSettings>().AnyAsync(cancellationToken))
        {
            db.Add(new ClinicSettings("Mostawsef", Currencies.Egp));
        }

        var systemSettings = await db.Set<SystemSettings>().SingleOrDefaultAsync(cancellationToken);
        var systemSettingsChanged = false;
        if (systemSettings is null)
        {
            systemSettings = new SystemSettings();
            systemSettings.Update(ScheduledBackupPaths.DefaultFolder, maintenanceMode: false);
            db.Add(systemSettings);
            systemSettingsChanged = true;
            logger.LogInformation("Created system settings with default backup folder {BackupPath}", systemSettings.BackupPath);
        }
        else if (string.IsNullOrWhiteSpace(systemSettings.BackupPath))
        {
            systemSettings.Update(ScheduledBackupPaths.DefaultFolder, systemSettings.MaintenanceMode);
            systemSettingsChanged = true;
            logger.LogInformation("Filled empty backup folder with default {BackupPath}", systemSettings.BackupPath);
        }

        var authCacheInvalidations = new List<Guid>();
        authCacheInvalidations.AddRange(await EnsureClinicUserAsync(db, hasher, logger, cancellationToken));
        authCacheInvalidations.AddRange(await RetireLegacyAdminAccountsAsync(db, hasher, logger, cancellationToken));

        foreach (var seed in seedUsers)
        {
            if (string.IsNullOrWhiteSpace(seed.Username) || string.IsNullOrWhiteSpace(seed.Password))
            {
                continue;
            }

            var username = User.NormalizeUsername(seed.Username);
            if (username == ClinicIdentity.Username || seed.Role != UserRole.Super)
            {
                // Clinic is seeded above; password login is Super-only — never seed Admin accounts from config.
                continue;
            }

            if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, cancellationToken))
            {
                continue;
            }

            var fullName = string.IsNullOrWhiteSpace(seed.FullName) ? username : seed.FullName;
            db.Users.Add(new User(username, fullName, SearchText.Normalize(fullName), hasher.Hash(seed.Password), seed.Role));
            logger.LogInformation("Seeded user {Username} with role {Role}", username, seed.Role);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Direct DbContext writes skip SystemSettingsService, so drop any cached row this host holds.
        if (systemSettingsChanged)
        {
            scope.ServiceProvider.GetService<ISystemSettingsCache>()?.Invalidate();
        }

        // Clinic repair / legacy Admin retirement bump TokenVersion; drop cache so old JWTs die immediately.
        if (authCacheInvalidations.Count > 0
            && scope.ServiceProvider.GetService<IUserAuthCache>() is { } authCache)
        {
            foreach (var userId in authCacheInvalidations)
            {
                authCache.Invalidate(userId);
            }
        }
    }

    /// <summary>
    /// Built-in Admin identity for passwordless clinic entry. Password is a random unusable hash (no password login).
    /// Repairs a soft-deleted, inactive, or wrong-role row that still owns the <c>clinic</c> username.
    /// Returns user ids whose auth cache must be cleared after save.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> EnsureClinicUserAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var username = ClinicIdentity.Username;
        var existing = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (existing is null)
        {
            var fullName = ClinicIdentity.FullName;
            db.Users.Add(new User(
                username,
                fullName,
                SearchText.Normalize(fullName),
                hasher.Hash(Guid.NewGuid().ToString("N")),
                UserRole.Admin));
            logger.LogInformation("Seeded clinic identity {Username}", username);
            return [];
        }

        var repaired = false;
        if (existing.IsDeleted)
        {
            var entry = db.Entry(existing);
            entry.Property(u => u.IsDeleted).CurrentValue = false;
            entry.Property(u => u.DeletedAt).CurrentValue = null;
            entry.Property(u => u.DeletedBy).CurrentValue = null;
            repaired = true;
        }

        if (existing.Role != UserRole.Admin)
        {
            existing.ChangeRole(UserRole.Admin);
            repaired = true;
        }

        if (!existing.IsActive)
        {
            existing.Activate();
            repaired = true;
        }

        if (!repaired)
        {
            return [];
        }

        // Drop any password that might have existed on a wrong-role row; clinic never signs in with one.
        existing.ChangePasswordHash(hasher.Hash(Guid.NewGuid().ToString("N")));
        existing.Rename(ClinicIdentity.FullName, SearchText.Normalize(ClinicIdentity.FullName));
        logger.LogInformation("Repaired clinic identity {Username}", username);
        return [existing.Id];
    }

    /// <summary>
    /// Password login is Super-only. Deactivate leftover Admin accounts (not the clinic identity) and
    /// bump their token version so any still-valid JWT stops on the next request.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> RetireLegacyAdminAccountsAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var legacy = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Role == UserRole.Admin
                && u.Username != ClinicIdentity.Username
                && !u.IsDeleted
                && u.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var user in legacy)
        {
            user.Deactivate();
            user.ChangePasswordHash(hasher.Hash(Guid.NewGuid().ToString("N")));
            logger.LogInformation("Retired legacy Admin account {Username}", user.Username);
        }

        return legacy.Select(u => u.Id).ToList();
    }
}

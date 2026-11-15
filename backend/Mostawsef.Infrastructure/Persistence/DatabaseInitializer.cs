using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Backups;
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

        foreach (var seed in seedUsers)
        {
            if (string.IsNullOrWhiteSpace(seed.Username) || string.IsNullOrWhiteSpace(seed.Password))
            {
                continue;
            }

            var username = User.NormalizeUsername(seed.Username);
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
    }
}

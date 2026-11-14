using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Interfaces;
using Mostawsef.Infrastructure.Persistence;
using Mostawsef.Infrastructure.Persistence.Context;
using Mostawsef.Infrastructure.Persistence.Repositories;
using Mostawsef.Infrastructure.Services;
using Mostawsef.Infrastructure.Services.Auth;
using Mostawsef.Infrastructure.Services.Backup;
using Mostawsef.Infrastructure.Services.Logging;
using Npgsql;

namespace Mostawsef.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Requires the host to register ICurrentUser as a singleton (the audit interceptor uses it)
    /// and to bind JwtOptions. The Api project does both.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // The one clock of the backend: inject TimeProvider instead of reading DateTime.UtcNow / DateTime.Today.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditInterceptor>();

        // Npgsql closes idle connections after 5 minutes; keep one open so the first request after a quiet spell
        // does not wait for a new connection and sign-in handshake. An explicit "Minimum Pool Size" wins.
        var npgsqlSettings = new NpgsqlConnectionStringBuilder(connectionString);
        if (npgsqlSettings.MinPoolSize == 0)
        {
            npgsqlSettings.MinPoolSize = 1;
            connectionString = npgsqlSettings.ConnectionString;
        }

        // Pooled: registers a scoped AppDbContext (repositories) and IDbContextFactory (log engines), both renting from one pool.
        services.AddPooledDbContextFactory<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<ISystemErrorRepository, SystemErrorRepository>();
        services.AddSingleton<INumberSequence, NumberSequenceRepository>();
        services.AddScoped<IClinicSettingsRepository, ClinicSettingsRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<ILabTestRepository, LabTestRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IBackupRepository, BackupRepository>();
        services.AddScoped<ISystemMaintenanceRepository, SystemMaintenanceRepository>();
        services.AddSingleton<IBackupExporter, DatabaseBackupExporter>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IUserAuthCache, UserAuthCache>();
        services.AddSingleton<ILoginThrottle, LoginThrottle>();
        services.AddSingleton<IClinicSettingsCache, ClinicSettingsCache>();
        services.AddSingleton<ISystemSettingsCache, SystemSettingsCache>();

        services.AddScoped<IActivityLogger, ActivityLogger>();
        services.AddScoped<ISystemErrorLogger, SystemErrorLogger>();

        return services;
    }
}

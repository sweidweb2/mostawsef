using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Interfaces;
using Mostawsef.Infrastructure.Persistence.Context;
using Mostawsef.Infrastructure.Persistence.Repositories;
using Mostawsef.Infrastructure.Services.Auth;
using Mostawsef.Infrastructure.Services.Logging;

namespace Mostawsef.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Requires an ICurrentUser implementation to be registered by the host (the Api project does this).</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString,
        JwtOptions jwtOptions)
    {
        // Registers both a scoped AppDbContext (repositories) and IDbContextFactory (log engines).
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<ISystemErrorRepository, SystemErrorRepository>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton(jwtOptions);
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<IActivityLogger, ActivityLogger>();
        services.AddScoped<ISystemErrorLogger, SystemErrorLogger>();

        return services;
    }
}

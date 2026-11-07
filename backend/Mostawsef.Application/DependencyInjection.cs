using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Modules.Auth;
using Mostawsef.Application.Modules.Logs;
using Mostawsef.Application.Modules.Users;

namespace Mostawsef.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddScoped<ISystemErrorService, SystemErrorService>();
        services.AddScoped<ILogRetentionService, LogRetentionService>();

        return services;
    }
}

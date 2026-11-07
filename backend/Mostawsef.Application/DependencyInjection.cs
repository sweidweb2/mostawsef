using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Modules.Auth;

namespace Mostawsef.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}

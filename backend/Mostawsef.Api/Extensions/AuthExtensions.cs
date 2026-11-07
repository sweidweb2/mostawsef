using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Auth;
using Mostawsef.Infrastructure.Services.Auth;

namespace Mostawsef.Api.Extensions;

public static class AuthPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string SuperOnly = nameof(SuperOnly);
    public const string LoginRateLimit = "login";
    public const string ClientErrorRateLimit = "client-errors";
}

public static class AuthExtensions
{
    /// <summary>Binds and validates JwtOptions ("Jwt" section; a bad Jwt:Key stops startup), then adds JWT bearer auth, policies and rate limits.</summary>
    public static IServiceCollection AddJwtAuth(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.UniqueName,
                    RoleClaimType = JwtClaims.Role,
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = RejectStaleUserAsync };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(Roles.Admin))
            .AddPolicy(AuthPolicies.SuperOnly, policy => policy.RequireRole(Roles.Super));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthPolicies.LoginRateLimit, context =>
            {
                var permitLimit = context.RequestServices.GetRequiredService<IConfiguration>()
                    .GetValue("RateLimit:LoginPermitLimit", 10);

                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });
            });
            options.AddPolicy(AuthPolicies.ClientErrorRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    /// <summary>A valid token stops working as soon as its user is deactivated, deleted or given another role.</summary>
    private static async Task RejectStaleUserAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();

        var user = Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId)
            ? await users.GetByIdAsync(userId, context.HttpContext.RequestAborted)
            : null;

        if (user is not { IsActive: true } || user.Role.ToString() != principal?.FindFirst(JwtClaims.Role)?.Value)
        {
            context.Fail("The user is no longer active or their role changed.");
        }
    }
}

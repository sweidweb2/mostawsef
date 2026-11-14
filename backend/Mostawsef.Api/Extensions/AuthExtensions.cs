using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Auth;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Services.Auth;

namespace Mostawsef.Api.Extensions;

public static class AuthPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string SuperOnly = nameof(SuperOnly);
    public const string LoginRateLimit = "login";
    public const string ClientErrorRateLimit = "client-errors";
    public const string HealthRateLimit = "health";
}

/// <summary>Requests per minute per client IP when <c>RateLimit:*</c> is not configured.</summary>
public static class RateLimitDefaults
{
    public const int LoginPerMinute = 10;
    public const int ClientErrorPerMinute = 30;
    public const int HealthPerMinute = 120;
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
                    .GetValue("RateLimit:LoginPermitLimit", RateLimitDefaults.LoginPerMinute);

                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });
            });
            options.AddPolicy(AuthPolicies.HealthRateLimit, context =>
            {
                var permitLimit = context.RequestServices.GetRequiredService<IConfiguration>()
                    .GetValue("RateLimit:HealthPermitLimit", RateLimitDefaults.HealthPerMinute);

                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1) });
            });
            options.AddPolicy(AuthPolicies.ClientErrorRateLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = RateLimitDefaults.ClientErrorPerMinute, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }

    /// <summary>A valid token stops working when its user is deactivated, deleted, given another role, or their password changes.</summary>
    private static async Task RejectStaleUserAsync(TokenValidatedContext context)
    {
        // Health is anonymous; skip the user lookup even if a client sent a Bearer token.
        if (context.HttpContext.Request.Path.StartsWithSegments("/api/health"))
        {
            return;
        }

        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId))
        {
            context.Fail("The user is no longer active or their credentials changed.");
            return;
        }

        var cache = context.HttpContext.RequestServices.GetRequiredService<IUserAuthCache>();
        if (!cache.TryGet(userId, out var status))
        {
            var generation = cache.Generation(userId);
            var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var loaded = await users.GetAuthStatusAsync(userId, context.HttpContext.RequestAborted);
            if (loaded is null)
            {
                context.Fail("The user is no longer active or their credentials changed.");
                return;
            }

            status = loaded.Value;
            cache.TrySet(userId, generation, status);
        }

        var tokenVersion = int.TryParse(principal?.FindFirst(JwtClaims.TokenVersion)?.Value, out var tv) ? tv : 0;
        if (!status.IsActive
            || !RoleMatches(status.Role, principal?.FindFirst(JwtClaims.Role)?.Value)
            || status.TokenVersion != tokenVersion)
        {
            context.Fail("The user is no longer active or their credentials changed.");
        }
    }

    private static bool RoleMatches(UserRole role, string? claim) =>
        claim is not null && Enum.TryParse(claim, ignoreCase: false, out UserRole parsed) && parsed == role;
}

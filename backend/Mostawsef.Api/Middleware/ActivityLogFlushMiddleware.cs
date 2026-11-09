using Mostawsef.Infrastructure.Services.Logging;

namespace Mostawsef.Api.Middleware;

/// <summary>Writes buffered activity logs after the endpoint finishes, before the response returns to the client.</summary>
public class ActivityLogFlushMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ActivityLogger activity)
    {
        try
        {
            await next(context);
        }
        finally
        {
            await activity.FlushAsync(CancellationToken.None);
        }
    }
}

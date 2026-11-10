using System.Diagnostics;

namespace Mostawsef.Api.Middleware;

/// <summary>
/// Times every HTTP request, adds a Server-Timing header the desktop can show, and logs the duration.
/// Skips the health check so the connection monitor does not flood the log.
/// </summary>
public class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsHealth(context.Request.Path))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        context.Response.OnStarting(() =>
        {
            // Milliseconds with one decimal, matching the Server-Timing "dur" unit.
            context.Response.Headers.Append("Server-Timing", $"app;dur={sw.Elapsed.TotalMilliseconds:F1}");
            return Task.CompletedTask;
        });

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs:F0}ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                sw.Elapsed.TotalMilliseconds);
        }
    }

    private static bool IsHealth(PathString path) =>
        path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);
}

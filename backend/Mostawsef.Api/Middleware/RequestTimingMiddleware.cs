using System.Diagnostics;
using System.Globalization;

namespace Mostawsef.Api.Middleware;

/// <summary>
/// Times every HTTP request, adds a Server-Timing header the desktop can show, and logs the duration.
/// Skips the health check so the connection monitor does not flood the log. Requests slower than
/// <see cref="SlowRequest"/> are warnings; the rest are Information, which production config hides
/// (writing a console line per request is slow on Windows).
/// </summary>
public partial class RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
{
    private static readonly object TimingKey = new();

    public static readonly TimeSpan SlowRequest = TimeSpan.FromSeconds(1);

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsHealth(context.Request.Path))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        context.Items[TimingKey] = sw;
        context.Response.OnStarting(static state =>
        {
            var http = (HttpContext)state!;
            var elapsed = ((Stopwatch)http.Items[TimingKey]!).Elapsed.TotalMilliseconds;
            // Milliseconds with one decimal, matching the Server-Timing "dur" unit.
            http.Response.Headers.Append("Server-Timing", string.Create(CultureInfo.InvariantCulture, $"app;dur={elapsed:F1}"));
            return Task.CompletedTask;
        }, context);

        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            if (sw.Elapsed >= SlowRequest)
            {
                LogSlowRequest(logger, context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, sw.ElapsedMilliseconds);
            }
            else if (logger.IsEnabled(LogLevel.Information))
            {
                LogRequest(logger, context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }

    private static bool IsHealth(PathString path) =>
        path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs}ms")]
    private static partial void LogRequest(ILogger logger, string method, string? path, int statusCode, long elapsedMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Slow HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs}ms")]
    private static partial void LogSlowRequest(ILogger logger, string method, string? path, int statusCode, long elapsedMs);
}

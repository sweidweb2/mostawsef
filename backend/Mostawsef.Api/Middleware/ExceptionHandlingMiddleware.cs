using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Application.Interfaces;

namespace Mostawsef.Api.Middleware;

/// <summary>
/// Last line of defense: every unhandled exception is recorded in system_errors and the client gets a
/// generic 500 problem response with a trace id (never the exception details).
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ISystemErrorLogger errors)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away; not an error.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await errors.LogAsync(ex, statusCode: StatusCodes.Status500InternalServerError);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Extensions = { ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier },
                },
                options: null,
                contentType: "application/problem+json");
        }
    }
}

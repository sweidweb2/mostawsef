using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Api.Middleware;

/// <summary>
/// Expected failures (AppException) become 400/404/409 problem responses with their error code.
/// Every other unhandled exception is recorded in system_errors and the client gets a generic 500
/// problem response with a trace id (never the exception details).
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
        catch (AppException ex) when (!context.Response.HasStarted)
        {
            await WriteProblemAsync(context, ToProblem(ex));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await errors.LogAsync(ex, statusCode: StatusCodes.Status500InternalServerError);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
            });
        }
    }

    private static ProblemDetails ToProblem(AppException ex)
    {
        var problem = ex.Field is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(new Dictionary<string, string[]> { [ex.Field] = [ex.Code] });

        (problem.Status, problem.Title) = ex.Kind switch
        {
            AppErrorKind.Validation => (StatusCodes.Status400BadRequest, "One or more validation errors occurred."),
            AppErrorKind.NotFound => (StatusCodes.Status404NotFound, "The record was not found."),
            _ => (StatusCodes.Status409Conflict, "The request conflicts with the current state."),
        };
        problem.Extensions[ProblemFields.Code] = ex.Kind == AppErrorKind.Validation ? ErrorCodes.Validation : ex.Code;
        return problem;
    }

    private static Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json");
    }
}

public static class ProblemFields
{
    /// <summary>Problem extension holding the ErrorCodes value.</summary>
    public const string Code = "code";
}

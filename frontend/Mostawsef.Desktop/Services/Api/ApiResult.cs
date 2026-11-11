using System;
using System.Collections.Generic;
using System.Net;

namespace Mostawsef.Desktop.Services.Api;

public enum ApiError
{
    None,

    /// <summary>The API could not be reached (not running, wrong address, timeout).</summary>
    Unreachable,
    Unauthorized,
    Forbidden,
    TooManyRequests,
    BadRequest,
    NotFound,

    /// <summary>409: the request clashes with existing data (e.g. a duplicate unique value).</summary>
    Conflict,
    Server,
}

/// <summary>
/// The body of a failed request: its ErrorCodes "code" and, per request field, the error codes
/// (e.g. FieldErrors["Username"] = ["username_taken"]). Field names match the request properties.
/// </summary>
public sealed record ApiProblem(string? Code, IReadOnlyDictionary<string, string[]> FieldErrors)
{
    public static readonly ApiProblem Empty = new(null, new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase));
}

/// <summary>What forms need from any result, whatever its value type.</summary>
public interface IApiResult
{
    bool IsSuccess { get; }

    ApiError Error { get; }

    /// <summary>Set for 4xx responses that carried a problem body.</summary>
    ApiProblem? Problem { get; }

    /// <summary>Response body on success when the API returned one; otherwise null.</summary>
    object? Body { get; }
}

public record ApiResult<T>(T? Value, ApiError Error, HttpStatusCode? StatusCode = null) : IApiResult
{
    public bool IsSuccess => Error == ApiError.None && Value is not null;

    public ApiProblem? Problem { get; init; }

    public object? Body => Value;

    public static ApiResult<T> Ok(T value) => new(value, ApiError.None, HttpStatusCode.OK);

    public static ApiResult<T> Fail(ApiError error, HttpStatusCode? statusCode = null, ApiProblem? problem = null) =>
        new(default, error, statusCode) { Problem = problem };
}

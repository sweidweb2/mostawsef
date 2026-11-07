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
    Server,
}

public record ApiResult<T>(T? Value, ApiError Error, HttpStatusCode? StatusCode = null)
{
    public bool IsSuccess => Error == ApiError.None && Value is not null;

    public static ApiResult<T> Ok(T value) => new(value, ApiError.None, HttpStatusCode.OK);

    public static ApiResult<T> Fail(ApiError error, HttpStatusCode? statusCode = null) => new(default, error, statusCode);
}

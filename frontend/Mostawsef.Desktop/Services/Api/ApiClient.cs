using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>
/// The only way the desktop app talks to the backend. Maps failures to ApiError.
/// The bearer token is added by AuthHeaderHandler in the HttpClient pipeline.
/// </summary>
public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync(() => http.GetAsync(path, cancellationToken), ReadJson<T>, cancellationToken);

    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync(() => http.PostAsJsonAsync(path, body, Json, cancellationToken), ReadJson<TResponse>, cancellationToken);

    /// <summary>POST without a response body (204 / 202). Value is true on success.</summary>
    public Task<ApiResult<bool>> PostAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync(() => http.PostAsJsonAsync(path, body, Json, cancellationToken), NoContent, cancellationToken);

    public Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync(() => http.PutAsJsonAsync(path, body, Json, cancellationToken), ReadJson<TResponse>, cancellationToken);

    public Task<ApiResult<bool>> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        SendAsync(() => http.DeleteAsync(path, cancellationToken), NoContent, cancellationToken);

    private async Task<ApiResult<T>> SendAsync<T>(
        Func<Task<HttpResponseMessage>> send,
        Func<HttpResponseMessage, CancellationToken, Task<ApiResult<T>>> read,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ApiResult<T>.Fail(ApiError.Unreachable);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return await read(response, cancellationToken);
            }

            var problem = (int)response.StatusCode is >= 400 and < 500 ? await ReadProblemAsync(response, cancellationToken) : null;
            return ApiResult<T>.Fail(MapError(response.StatusCode), response.StatusCode, problem);
        }
    }

    /// <summary>Reads "code" and "errors" from a problem body; null when the body is not a problem.</summary>
    private static async Task<ApiProblem?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in e.EnumerateObject())
                {
                    if (field.Value.ValueKind == JsonValueKind.Array)
                    {
                        errors[field.Name] = field.Value.EnumerateArray()
                            .Where(v => v.ValueKind == JsonValueKind.String)
                            .Select(v => v.GetString()!)
                            .ToArray();
                    }
                }
            }

            return new ApiProblem(code, errors);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<ApiResult<T>> ReadJson<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
        return value is null ? ApiResult<T>.Fail(ApiError.Server, response.StatusCode) : ApiResult<T>.Ok(value);
    }

    private static Task<ApiResult<bool>> NoContent(HttpResponseMessage response, CancellationToken cancellationToken) =>
        Task.FromResult(ApiResult<bool>.Ok(true));

    private static ApiError MapError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => ApiError.Unauthorized,
        HttpStatusCode.Forbidden => ApiError.Forbidden,
        HttpStatusCode.TooManyRequests => ApiError.TooManyRequests,
        HttpStatusCode.BadRequest => ApiError.BadRequest,
        HttpStatusCode.NotFound => ApiError.NotFound,
        HttpStatusCode.Conflict => ApiError.Conflict,
        _ => ApiError.Server,
    };
}

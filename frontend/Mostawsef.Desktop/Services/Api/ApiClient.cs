using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>
/// The only way the desktop app talks to the backend. Maps failures to ApiError.
/// The bearer token is added by AuthHeaderHandler in the HttpClient pipeline.
/// </summary>
public class ApiClient(Lazy<HttpClient> http, PerformanceMonitor perf)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Raised (on the calling thread) whenever a call fails because the API could not be reached.</summary>
    public event EventHandler? Unreachable;

    private HttpClient Http => http.Value;

    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync("GET", path, () => Http.GetAsync(path, cancellationToken), ReadJson<T>, cancellationToken);

    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync("POST", path, () => Http.PostAsJsonAsync(path, body, Json, cancellationToken), ReadJson<TResponse>, cancellationToken);

    /// <summary>POST without a response body (204 / 202). Value is true on success.</summary>
    public Task<ApiResult<bool>> PostAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync("POST", path, () => Http.PostAsJsonAsync(path, body, Json, cancellationToken), NoContent, cancellationToken);

    public Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync("PUT", path, () => Http.PutAsJsonAsync(path, body, Json, cancellationToken), ReadJson<TResponse>, cancellationToken);

    public Task<ApiResult<bool>> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        SendAsync("DELETE", path, () => Http.DeleteAsync(path, cancellationToken), NoContent, cancellationToken);

    private async Task<ApiResult<T>> SendAsync<T>(
        string method,
        string path,
        Func<Task<HttpResponseMessage>> send,
        Func<HttpResponseMessage, CancellationToken, Task<ApiResult<T>>> read,
        CancellationToken cancellationToken)
    {
        var track = perf.IsEnabled;
        var sw = track ? Stopwatch.StartNew() : null;
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException)
        {
            if (sw is not null)
            {
                sw.Stop();
                RecordApi(method, path, sw.Elapsed, "unreachable");
            }

            // Real transport failure — flip the connection banner.
            Unreachable?.Invoke(this, EventArgs.Empty);
            return ApiResult<T>.Fail(ApiError.Unreachable);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (sw is not null)
            {
                sw.Stop();
                RecordApi(method, path, sw.Elapsed, "timeout");
            }

            // Request timed out on a reachable server — fail the call, do not mark the app offline.
            return ApiResult<T>.Fail(ApiError.Unreachable);
        }

        using (response)
        {
            if (sw is not null)
            {
                sw.Stop();
                RecordApi(method, path, sw.Elapsed, response);
            }

            if (response.IsSuccessStatusCode)
            {
                return await read(response, cancellationToken);
            }

            var problem = (int)response.StatusCode is >= 400 and < 500 ? await ReadProblemAsync(response, cancellationToken) : null;
            return ApiResult<T>.Fail(MapError(response.StatusCode), response.StatusCode, problem);
        }
    }

    private void RecordApi(string method, string path, TimeSpan duration, string detail)
    {
        if (!perf.IsEnabled || path.Contains("health", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Strip query strings so list filters do not explode the overlay.
        var cut = path.IndexOf('?', StringComparison.Ordinal);
        var route = cut >= 0 ? path[..cut] : path;
        perf.Record("Api", string.Concat(method, " ", route), duration, detail);
    }

    private void RecordApi(string method, string path, TimeSpan duration, HttpResponseMessage response)
    {
        if (!perf.IsEnabled)
        {
            return;
        }

        var serverMs = ReadServerTimingMs(response);
        var detail = serverMs is { } ms
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)response.StatusCode}, server {ms:F0}ms")
            : ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        RecordApi(method, path, duration, detail);
    }

    /// <summary>Reads app;dur=N from Server-Timing when the API sent it.</summary>
    private static double? ReadServerTimingMs(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Server-Timing", out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            var dur = value.IndexOf("dur=", StringComparison.OrdinalIgnoreCase);
            if (dur < 0)
            {
                continue;
            }

            var start = dur + 4;
            var end = start;
            while (end < value.Length && (char.IsDigit(value[end]) || value[end] is '.'))
            {
                end++;
            }

            if (end > start
                && double.TryParse(value.AsSpan(start, end - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
            {
                return ms;
            }
        }

        return null;
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
        try
        {
            var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
            return value is null ? ApiResult<T>.Fail(ApiError.Server, response.StatusCode) : ApiResult<T>.Ok(value);
        }
        catch (JsonException)
        {
            return ApiResult<T>.Fail(ApiError.Server, response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ApiResult<T>.Fail(ApiError.Unreachable);
        }
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

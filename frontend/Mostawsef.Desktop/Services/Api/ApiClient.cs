using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>
/// The only way the desktop app talks to the backend. Maps failures to ApiError.
/// The bearer token is added by AuthHeaderHandler in the HttpClient pipeline.
/// </summary>
public class ApiClient(Lazy<HttpClient> http, PerformanceMonitor perf, TimeSpan? requestTimeout = null)
{
    /// <summary>
    /// Limit for the few calls that work through a whole table on the server (export a backup, delete a whole log,
    /// reset the data). Every other call keeps the normal limit, <see cref="ServiceRegistration.RequestTimeout"/>.
    /// </summary>
    public static readonly TimeSpan LongRequestTimeout = TimeSpan.FromMinutes(10);

    private readonly TimeSpan _requestTimeout = requestTimeout ?? ServiceRegistration.RequestTimeout;

    /// <summary>Compile-time metadata for the contracts first (fast first call per type), reflection for anything else.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(ContractsJsonContext.Default, new DefaultJsonTypeInfoResolver()),
    };

    /// <summary>Raised (on the calling thread) whenever a call fails because the API could not be reached.</summary>
    public event EventHandler? Unreachable;

    private HttpClient Http => http.Value;

    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync("GET", path, token => Http.GetAsync(path, token), ReadJson<T>, null, cancellationToken);

    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync("POST", path, token => Http.PostAsJsonAsync(path, body, Json, token), ReadJson<TResponse>, null, cancellationToken);

    /// <summary>A POST with its own limit: <see cref="LongRequestTimeout"/> for work over a whole table.</summary>
    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string path, TRequest body, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        SendAsync("POST", path, token => Http.PostAsJsonAsync(path, body, Json, token), ReadJson<TResponse>, timeout, cancellationToken);

    /// <summary>POST without a response body (204 / 202). Value is true on success.</summary>
    public Task<ApiResult<bool>> PostAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync("POST", path, token => Http.PostAsJsonAsync(path, body, Json, token), NoContent, null, cancellationToken);

    public Task<ApiResult<TResponse>> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync("PUT", path, token => Http.PutAsJsonAsync(path, body, Json, token), ReadJson<TResponse>, null, cancellationToken);

    public Task<ApiResult<bool>> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        SendAsync("DELETE", path, token => Http.DeleteAsync(path, token), NoContent, null, cancellationToken);

    /// <summary>DELETE that returns a JSON body (e.g. the updated order after removing a payment).</summary>
    public Task<ApiResult<TResponse>> DeleteAsync<TResponse>(string path, CancellationToken cancellationToken = default) =>
        SendAsync("DELETE", path, token => Http.DeleteAsync(path, token), ReadJson<TResponse>, null, cancellationToken);

    /// <summary>A DELETE with its own limit: <see cref="LongRequestTimeout"/> for work over a whole table.</summary>
    public Task<ApiResult<bool>> DeleteAsync(string path, TimeSpan timeout, CancellationToken cancellationToken = default) =>
        SendAsync("DELETE", path, token => Http.DeleteAsync(path, token), NoContent, timeout, cancellationToken);

    /// <summary>
    /// Downloads the response body into <paramref name="destinationPath"/>. The bytes go to "path.part" first and replace the
    /// destination only when the download is complete, so a failure never leaves a half-written file under the real name.
    /// Value is true on success. Uses <see cref="LongRequestTimeout"/>: the server builds the whole file before answering.
    /// </summary>
    public Task<ApiResult<bool>> DownloadAsync(string path, string destinationPath, CancellationToken cancellationToken = default) =>
        SendAsync(
            "GET",
            path,
            token => Http.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, token),
            (response, token) => SaveToFileAsync(response, destinationPath, token),
            LongRequestTimeout,
            cancellationToken);

    /// <summary>
    /// POSTs a local file as multipart form field <paramref name="formField"/> and reads a JSON body.
    /// Uses <see cref="LongRequestTimeout"/> for catalog-sized imports.
    /// </summary>
    public Task<ApiResult<TResponse>> PostFileAsync<TResponse>(
        string path,
        string filePath,
        string formField = "file",
        CancellationToken cancellationToken = default) =>
        SendAsync(
            "POST",
            path,
            token => PostMultipartFileAsync(path, filePath, formField, token),
            ReadJson<TResponse>,
            LongRequestTimeout,
            cancellationToken);

    private async Task<HttpResponseMessage> PostMultipartFileAsync(
        string path, string filePath, string formField, CancellationToken cancellationToken)
    {
        // Import files are capped at 2 MB on the API; reading into memory keeps ownership simple for multipart.
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), formField, Path.GetFileName(filePath));
        return await Http.PostAsync(path, content, cancellationToken);
    }

    private static async Task<ApiResult<bool>> SaveToFileAsync(HttpResponseMessage response, string destinationPath, CancellationToken cancellationToken)
    {
        var partPath = destinationPath + ".part";
        try
        {
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            File.Move(partPath, destinationPath, overwrite: true);
            return ApiResult<bool>.Ok(true);
        }
        finally
        {
            File.Delete(partPath);
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        string method,
        string path,
        Func<CancellationToken, Task<HttpResponseMessage>> send,
        Func<HttpResponseMessage, CancellationToken, Task<ApiResult<T>>> read,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        // The limit lives here, not in HttpClient.Timeout, so a single call can be given longer (the HttpClient has none).
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout ?? _requestTimeout);

        var track = perf.IsEnabled;
        var sw = track ? Stopwatch.StartNew() : null;
        HttpResponseMessage response;
        try
        {
            response = await send(limit.Token);
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
                try
                {
                    return await read(response, limit.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // The limit ran out while the body was still arriving (a long download).
                    return ApiResult<T>.Fail(ApiError.Unreachable);
                }
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

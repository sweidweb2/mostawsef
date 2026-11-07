using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>
/// The only way the desktop app talks to the backend. Adds the bearer token and maps failures to ApiError.
/// </summary>
public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Raised when an authenticated request is rejected with 401 (token expired or revoked).</summary>
    public event EventHandler? Unauthorized;

    public void SetAccessToken(string? token) =>
        http.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);

    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(() => http.GetAsync(path, cancellationToken), cancellationToken);

    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => http.PostAsJsonAsync(path, body, Json, cancellationToken), cancellationToken);

    private async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
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
                var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
                return value is null ? ApiResult<T>.Fail(ApiError.Server, response.StatusCode) : ApiResult<T>.Ok(value);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && http.DefaultRequestHeaders.Authorization is not null)
            {
                Unauthorized?.Invoke(this, EventArgs.Empty);
            }

            return ApiResult<T>.Fail(MapError(response.StatusCode), response.StatusCode);
        }
    }

    private static ApiError MapError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => ApiError.Unauthorized,
        HttpStatusCode.Forbidden => ApiError.Forbidden,
        HttpStatusCode.TooManyRequests => ApiError.TooManyRequests,
        HttpStatusCode.BadRequest => ApiError.BadRequest,
        HttpStatusCode.NotFound => ApiError.NotFound,
        _ => ApiError.Server,
    };
}

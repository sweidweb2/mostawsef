using System.Net;
using System.Net.Http;
using System.Text;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Api;

[Collection(UiTestCollection.Name)]
public class ApiClientTests : UiTestBase
{
    [AvaloniaFact]
    public async Task Invalid_json_body_is_a_server_error_not_an_exception()
    {
        using var app = new TestServices();
        app.Api.On(HttpMethod.Get, "/api/health", _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json"),
        });

        var result = await app.Get<ApiClient>().GetAsync<HealthDto>("api/health", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApiError.Server, result.Error);
    }

    [AvaloniaTheory]
    [InlineData(HttpStatusCode.BadRequest, ApiError.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized, ApiError.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, ApiError.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, ApiError.NotFound)]
    [InlineData(HttpStatusCode.Conflict, ApiError.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests, ApiError.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError, ApiError.Server)]
    public async Task Error_status_codes_map_to_api_errors(HttpStatusCode status, ApiError expected)
    {
        using var app = new TestServices();
        app.Api.RespondWithStatus(status);

        var result = await app.Get<ApiClient>().DeleteAsync("api/anything", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
        Assert.Equal(status, result.StatusCode);
    }

    [AvaloniaFact]
    public async Task A_network_failure_is_unreachable()
    {
        using var app = new TestServices();
        app.Api.FailWithNetworkError();

        var result = await app.Get<ApiClient>().DeleteAsync("api/anything", TestContext.Current.CancellationToken);

        Assert.Equal(ApiError.Unreachable, result.Error);
    }

    [AvaloniaFact]
    public async Task A_request_that_hits_the_time_limit_is_a_timeout_not_unreachable()
    {
        var handler = new FakeApiHandler();
        handler.On(HttpMethod.Delete, "/api/anything", async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };

        var result = await new ApiClient(
                new Lazy<HttpClient>(() => http),
                new Mostawsef.Desktop.Services.Diagnostics.PerformanceMonitor { IsEnabled = false, WriteToFile = false },
                requestTimeout: TimeSpan.FromMilliseconds(50))
            .DeleteAsync("api/anything", TestContext.Current.CancellationToken);

        Assert.Equal(ApiError.Timeout, result.Error);
    }
}

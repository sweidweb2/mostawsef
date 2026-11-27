using System.Diagnostics;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.Tests.Api;

public class ConnectTimeoutTests
{
    [Fact]
    public async Task An_api_pc_that_never_answers_fails_after_the_connect_timeout_not_the_request_timeout()
    {
        var handler = ServiceRegistration.CreateNetworkHandler();
        handler.ConnectCallback = async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("The connection never opens.");
        };
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://clinic-server:5270/"),
            Timeout = Timeout.InfiniteTimeSpan, // as in ServiceRegistration: ApiClient applies the request limit
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await new ApiClient(new Lazy<HttpClient>(() => http), new PerformanceMonitor { IsEnabled = false, WriteToFile = false })
            .GetAsync<UserDto>("api/clinic-settings", TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.Equal(ApiError.Unreachable, result.Error);
        Assert.True(ServiceRegistration.ConnectTimeout < ServiceRegistration.RequestTimeout / 3);
        Assert.True(stopwatch.Elapsed < ServiceRegistration.RequestTimeout / 2, $"Failed after {stopwatch.Elapsed}");
    }
}

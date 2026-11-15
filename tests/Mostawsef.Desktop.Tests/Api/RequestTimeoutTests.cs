using System.Net;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.Tests.Api;

/// <summary>
/// Normal calls give up after the request limit; whole-table work (backup export, delete a whole log, data reset)
/// may take longer, because the server finishes the work before it answers.
/// </summary>
public class RequestTimeoutTests
{
    private static readonly TimeSpan ShortLimit = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan ServerWork = TimeSpan.FromMilliseconds(800);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ApiClient SlowServer(HttpStatusCode status, string body)
    {
        var http = new HttpClient(new SlowHandler(status, body))
        {
            BaseAddress = new Uri("http://localhost:5270/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return new ApiClient(
            new Lazy<HttpClient>(() => http), new PerformanceMonitor { IsEnabled = false, WriteToFile = false }, ShortLimit);
    }

    [Fact]
    public async Task A_normal_call_stops_at_the_request_limit()
    {
        var result = await SlowServer(HttpStatusCode.OK, "{}").GetAsync<UserDto>("api/clinic-settings", Ct);

        Assert.Equal(ApiError.Unreachable, result.Error);
    }

    [Fact]
    public async Task A_long_call_is_not_cut_off_at_the_normal_request_limit()
    {
        var result = await SlowServer(HttpStatusCode.NoContent, string.Empty)
            .DeleteAsync("api/activity-logs", ApiClient.LongRequestTimeout, Ct);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task A_download_waits_for_the_server_to_build_the_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mostawsef-test-{Guid.NewGuid():N}.csv");
        try
        {
            var result = await SlowServer(HttpStatusCode.OK, "a,b\r\n1,2\r\n").DownloadAsync("api/backups/x/export", path, Ct);

            Assert.True(result.IsSuccess);
            Assert.Equal("a,b\r\n1,2\r\n", await File.ReadAllTextAsync(path, Ct));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Long_calls_get_more_time_than_normal_ones()
    {
        Assert.True(ApiClient.LongRequestTimeout > ServiceRegistration.RequestTimeout * 10);
    }

    /// <summary>Answers after <see cref="ServerWork"/>, like a server busy with a whole table.</summary>
    private sealed class SlowHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(ServerWork, cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}

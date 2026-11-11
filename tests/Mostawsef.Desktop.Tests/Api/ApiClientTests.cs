using System.Net;
using Avalonia.Headless.XUnit;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Api;

[Collection(UiTestCollection.Name)]
public class ApiClientTests : UiTestBase
{
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
}

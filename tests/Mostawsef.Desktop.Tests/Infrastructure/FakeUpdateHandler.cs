using System.Net;
using System.Text;
using System.Text.Json;
using Mostawsef.Desktop.Services.Updates;

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>Stands in for the GitHub update feed HTTP calls.</summary>
public sealed class FakeUpdateHandler : HttpMessageHandler
{
    public string? JsonBody { get; set; }

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public Exception? Error { get; set; }

    public int RequestCount { get; private set; }

    public void RespondWith(UpdateFeed feed) =>
        JsonBody = JsonSerializer.Serialize(feed);

    public void FailWithNetworkError() => Error = new HttpRequestException("Connection refused");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        if (Error is not null)
        {
            throw Error;
        }

        return Task.FromResult(new HttpResponseMessage(StatusCode)
        {
            Content = new StringContent(JsonBody ?? "{}", Encoding.UTF8, "application/json"),
        });
    }
}

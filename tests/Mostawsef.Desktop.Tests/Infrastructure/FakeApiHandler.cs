using System.Net;
using System.Net.Http.Json;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>Stands in for the API: each test decides what the "server" answers.</summary>
public class FakeApiHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    public List<HttpRequestMessage> Requests { get; } = [];

    public void RespondToLoginWith(string role, string username = "user@test.local") =>
        Respond = _ => Json(LoginResponseFor(role, username));

    public void RespondWithStatus(HttpStatusCode status) => Respond = _ => new HttpResponseMessage(status);

    public void FailWithNetworkError() => Respond = _ => throw new HttpRequestException("Connection refused");

    public static LoginResponse LoginResponseFor(string role, string username = "user@test.local") =>
        new("test-token", DateTime.UtcNow.AddHours(8), new UserDto(Guid.NewGuid(), username, role));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Respond(request));
    }

    private static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}

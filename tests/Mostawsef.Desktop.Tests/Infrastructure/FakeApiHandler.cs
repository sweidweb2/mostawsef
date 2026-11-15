using System.Net;
using System.Net.Http.Json;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>
/// Stands in for the API: each test decides what the "server" answers.
/// Routes (On...) are matched first by method and path prefix (longest prefix wins); everything else goes to Respond.
/// </summary>
public class FakeApiHandler : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string PathPrefix, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond)> _routes = [];

    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    public List<HttpRequestMessage> Requests { get; } = [];

    public void RespondToLoginWith(string role, string username = "user@test.local") =>
        Respond = _ => Json(LoginResponseFor(role, username));

    public void RespondWithStatus(HttpStatusCode status) => Respond = _ => new HttpResponseMessage(status);

    public void FailWithNetworkError() => Respond = _ => throw new HttpRequestException("Connection refused");

    /// <summary>Answers requests to <paramref name="pathPrefix"/> (e.g. "/api/activity-logs/stats") with JSON.</summary>
    public FakeApiHandler OnGet<T>(string pathPrefix, T body) => On(HttpMethod.Get, pathPrefix, _ => Json(body));

    public FakeApiHandler On(HttpMethod method, string pathPrefix, Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        On(method, pathPrefix, (request, _) => Task.FromResult(respond(request)));

    /// <summary>For answers that arrive later (e.g. a slow server); the token is the one the app's request carries.</summary>
    public FakeApiHandler On(HttpMethod method, string pathPrefix, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _routes.Add((method, pathPrefix, respond));
        return this;
    }

    public FakeApiHandler On(HttpMethod method, string pathPrefix, HttpStatusCode status) =>
        On(method, pathPrefix, _ => new HttpResponseMessage(status));

    public IEnumerable<HttpRequestMessage> RequestsTo(HttpMethod method, string pathPrefix) =>
        Requests.Where(r => r.Method == method && r.RequestUri!.AbsolutePath.StartsWith(pathPrefix, StringComparison.Ordinal));

    public static LoginResponse LoginResponseFor(string role, string username = "user@test.local") =>
        new("test-token", DateTime.UtcNow.AddHours(8), new UserDto(Guid.NewGuid(), username, role), ExpiresInSeconds: 8 * 3600);

    public static HttpResponseMessage Json<T>(T body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    /// <summary>A problem response like the API's: Problem(HttpStatusCode.Conflict, "username_taken", ("Username", "username_taken")).</summary>
    public static HttpResponseMessage Problem(HttpStatusCode status, string code, params (string Field, string Code)[] errors) =>
        new(status)
        {
            Content = JsonContent.Create(new Dictionary<string, object>
            {
                ["status"] = (int)status,
                ["code"] = code,
                ["errors"] = errors.ToDictionary(e => e.Field, e => new[] { e.Code }),
            }),
        };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        var path = request.RequestUri!.AbsolutePath;
        var route = _routes
            .Where(r => r.Method == request.Method && path.StartsWith(r.PathPrefix, StringComparison.Ordinal))
            .OrderByDescending(r => r.PathPrefix.Length)
            .Select(r => r.Respond)
            .FirstOrDefault();

        return route is null ? Task.FromResult(Respond(request)) : route(request, cancellationToken);
    }
}

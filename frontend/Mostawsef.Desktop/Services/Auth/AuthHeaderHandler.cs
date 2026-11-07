using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Mostawsef.Desktop.Services.Auth;

/// <summary>
/// HttpClient pipeline step: adds the session's bearer token to each request, and expires the session
/// when the API rejects that token with 401. ApiClient knows nothing about tokens.
/// </summary>
public sealed class AuthHeaderHandler(SessionService session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = session.AccessToken;
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (token is not null && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            session.Expire(token);
        }

        return response;
    }
}

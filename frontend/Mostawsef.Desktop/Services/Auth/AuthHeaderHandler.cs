using System;
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
    /// <summary>Anonymous health checks must not send a Bearer token (avoids a DB user lookup on every poll).</summary>
    internal static bool OmitsBearer(HttpRequestMessage request) =>
        request.RequestUri is { AbsolutePath: var path }
        && path.Equals("/api/health", StringComparison.OrdinalIgnoreCase);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = session.AccessToken;
        var attachBearer = token is not null && !OmitsBearer(request);
        if (attachBearer)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (attachBearer && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            session.Expire(token!);
        }

        return response;
    }
}

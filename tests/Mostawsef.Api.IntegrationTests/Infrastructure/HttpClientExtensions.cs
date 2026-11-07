using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Api.IntegrationTests.Infrastructure;

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("api/auth/login", new LoginRequest(username, password), TestContext.Current.CancellationToken);

    public static async Task<LoginResponse> LoginAsAsync(this HttpClient client, TestUser user)
    {
        var response = await client.LoginAsync(user.Username, user.Password);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken))!;
    }

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Users;

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

    /// <summary>A client already signed in as the given user (seeded Admin/Super tokens are cached on the factory).</summary>
    public static async Task<HttpClient> CreateClientAsAsync(this ApiFactory factory, TestUser user) =>
        factory.CreateClient().WithBearer(await factory.GetAccessTokenAsync(user));

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken))!;
    }

    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>Creates a new user through the API (the client must be signed in as Super) and returns it with its credentials.</summary>
    public static async Task<(UserDetailsDto User, TestUser Login)> CreateUserAsync(
        this HttpClient super, string role = Roles.Admin, string? fullName = null)
    {
        var login = new TestUser($"{Unique("user")}@test.local", "Created#Pass1", role);
        var response = await super.PostAsJsonAsync(
            "api/users",
            new CreateUserRequest { FullName = fullName ?? "Test User", Username = login.Username, Password = login.Password, Role = role },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<UserDetailsDto>(TestContext.Current.CancellationToken))!, login);
    }
}

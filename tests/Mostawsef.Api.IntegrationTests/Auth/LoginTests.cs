using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class LoginTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<string> Roles => [TestUsers.Admin.Role, TestUsers.Super.Role];

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task Valid_credentials_return_a_token_and_the_users_role(string role)
    {
        var user = role == TestUsers.Admin.Role ? TestUsers.Admin : TestUsers.Super;

        var login = await _client.LoginAsAsync(user);

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.True(login.ExpiresAt > DateTime.UtcNow);
        Assert.True(login.ExpiresInSeconds > 0);
        Assert.Equal(user.Username, login.User.Username);
        Assert.Equal(role, login.User.Role);
    }

    [Fact]
    public async Task Username_is_case_and_whitespace_insensitive()
    {
        var response = await _client.LoginAsync("  " + TestUsers.Admin.Username.ToUpperInvariant() + " ", TestUsers.Admin.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var response = await _client.LoginAsync(TestUsers.Admin.Username, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_returns_401()
    {
        var response = await _client.LoginAsync("nobody@test.local", "whatever");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("admin@test.local", "")]
    [InlineData("   ", "password")]
    [InlineData("admin@test.local", "   ")]
    public async Task Missing_fields_return_400(string username, string password)
    {
        var response = await _client.LoginAsync(username, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_password_returns_a_validation_problem_naming_the_field()
    {
        var response = await _client.LoginAsync(TestUsers.Admin.Username, "");
        var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = body.RootElement.GetProperty("errors").EnumerateObject().Select(e => e.Name);
        Assert.Contains(fields, f => f.Equals("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, f => f.Equals("Username", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Failed_login_does_not_reveal_whether_the_user_exists()
    {
        var unknown = await _client.LoginAsync("nobody@test.local", "whatever");
        var wrongPassword = await _client.LoginAsync(TestUsers.Admin.Username, "whatever");

        Assert.Equal(unknown.StatusCode, wrongPassword.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ContinueWith(t => Title(t.Result)),
            await wrongPassword.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ContinueWith(t => Title(t.Result)));
    }

    [Fact]
    public async Task Login_response_never_contains_the_password_or_hash()
    {
        var response = await _client.LoginAsync(TestUsers.Admin.Username, TestUsers.Admin.Password);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(TestUsers.Admin.Password, body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken));
    }

    private static string? Title(string problemJson) =>
        System.Text.Json.JsonDocument.Parse(problemJson).RootElement.GetProperty("title").GetString();
}

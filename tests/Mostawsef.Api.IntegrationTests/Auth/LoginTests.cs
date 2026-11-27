using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class LoginTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Super_credentials_return_a_token_and_the_super_role()
    {
        var login = await _client.LoginAsAsync(TestUsers.Super);

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.True(login.ExpiresAt > DateTime.UtcNow);
        Assert.True(login.ExpiresInSeconds > 0);
        Assert.Equal(TestUsers.Super.Username, login.User.Username);
        Assert.Equal(Roles.Super, login.User.Role);
    }

    [Fact]
    public async Task Clinic_session_returns_an_admin_token_for_the_clinic_identity()
    {
        var login = await _client.OpenClinicSessionAsync();

        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.Equal(ClinicIdentity.Username, login.User.Username);
        Assert.Equal(Roles.Admin, login.User.Role);
        Assert.Equal(ClinicIdentity.FullName, login.User.FullName);
    }

    [Fact]
    public async Task Clinic_access_is_available_when_maintenance_is_off()
    {
        var response = await _client.GetClinicAccessAsync();
        var body = await response.Content.ReadFromJsonAsync<ClinicAccessResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.Available);
    }

    [Fact]
    public async Task Username_is_case_and_whitespace_insensitive()
    {
        var response = await _client.LoginAsync("  " + TestUsers.Super.Username.ToUpperInvariant() + " ", TestUsers.Super.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var response = await _client.LoginAsync(TestUsers.Super.Username, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Clinic_identity_cannot_sign_in_with_a_password()
    {
        var response = await _client.LoginAsync(ClinicIdentity.Username, "anything");

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
    [InlineData("super@test.local", "")]
    [InlineData("   ", "password")]
    [InlineData("super@test.local", "   ")]
    public async Task Missing_fields_return_400(string username, string password)
    {
        var response = await _client.LoginAsync(username, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_password_returns_a_validation_problem_naming_the_field()
    {
        var response = await _client.LoginAsync(TestUsers.Super.Username, "");
        var body = System.Text.Json.JsonElement.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = body.GetProperty("errors").EnumerateObject().Select(e => e.Name);
        Assert.Contains(fields, f => f.Equals("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, f => f.Equals("Username", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Failed_login_does_not_reveal_whether_the_user_exists()
    {
        var unknown = await _client.LoginAsync("nobody@test.local", "whatever");
        var wrongPassword = await _client.LoginAsync(TestUsers.Super.Username, "whatever");

        Assert.Equal(unknown.StatusCode, wrongPassword.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ContinueWith(t => Title(t.Result)),
            await wrongPassword.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ContinueWith(t => Title(t.Result)));
    }

    [Fact]
    public async Task Login_response_never_contains_the_password_or_hash()
    {
        var response = await _client.LoginAsync(TestUsers.Super.Username, TestUsers.Super.Password);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(TestUsers.Super.Password, body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Clinic_session_response_never_contains_a_password_or_hash()
    {
        var response = await _client.PostAsJsonAsync("api/auth/clinic-session", new { }, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"password\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AQAAAA", body, StringComparison.Ordinal); // Identity v3 hash prefix
        // The only password-related field is the flag, and the clinic identity never has to change one.
        Assert.Contains("\"mustChangePassword\":false", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clinic_session_from_a_non_loopback_address_returns_403()
    {
        var context = await factory.Server.SendAsync(ctx =>
        {
            ctx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
            ctx.Request.Method = HttpMethods.Post;
            ctx.Request.Path = "/api/auth/clinic-session";
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("X-Forwarded-For", "203.0.113.10")]
    [InlineData("Forwarded", "for=203.0.113.10")]
    public async Task Clinic_session_through_an_unresolved_proxy_returns_403(string header, string value)
    {
        // A reverse proxy on the same PC connects from loopback; without ForwardedHeaders the real client is only in this header.
        var context = await factory.Server.SendAsync(ctx =>
        {
            ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
            ctx.Request.Method = HttpMethods.Post;
            ctx.Request.Path = "/api/auth/clinic-session";
            ctx.Request.Headers[header] = value;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static string? Title(string problemJson) =>
        System.Text.Json.JsonElement.Parse(problemJson).GetProperty("title").GetString();
}

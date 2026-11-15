using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Common;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Users;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Users;

[Collection(ApiCollection.Name)]
public class UsersTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Super_creates_a_user_who_can_then_sign_in()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var username = $"{HttpClientExtensions.Unique("new")}@test.local";

        var response = await super.PostAsJsonAsync(
            "api/users",
            new CreateUserRequest { FullName = "  Sara Mahmoud ", Username = "  " + username.ToUpperInvariant(), Password = "Welcome#123", Role = Roles.Admin },
            Ct);
        var created = await response.Content.ReadFromJsonAsync<UserDetailsDto>(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"api/users/{created!.Id}", response.Headers.Location!.ToString());
        Assert.Equal(username, created.Username);
        Assert.Equal("Sara Mahmoud", created.FullName);
        Assert.Equal(Roles.Admin, created.Role);
        Assert.True(created.IsActive);
        Assert.Equal(0, created.Version);
        Assert.Equal(TestUsers.Super.Username, created.CreatedBy);
        Assert.Null(created.UpdatedAt);

        var login = await factory.CreateClient().LoginAsync(username, "Welcome#123");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var signedIn = (await login.Content.ReadFromJsonAsync<LoginResponse>(Ct))!.User;
        Assert.Equal("Sara Mahmoud", signedIn.FullName);
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_an_error_code_per_field()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var response = await super.PostAsJsonAsync(
            "api/users",
            new CreateUserRequest { FullName = "   ", Username = "has space", Password = "short", Role = "Boss" },
            Ct);
        var problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(ErrorCodes.Validation, problem.Code);
        Assert.Equal([ErrorCodes.Required], problem.Errors["FullName"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["Username"]);
        Assert.Equal([$"min_length|{UserRules.PasswordMinLength}"], problem.Errors["Password"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["Role"]);
    }

    [Fact]
    public async Task Too_long_values_report_the_limit()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var response = await super.PostAsJsonAsync(
            "api/users",
            new CreateUserRequest { FullName = new string('x', 101), Username = "ok@test.local", Password = "Welcome#123" },
            Ct);
        var problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal([$"max_length|{UserRules.FullNameMaxLength}"], problem.Errors["FullName"]);
    }

    [Fact]
    public async Task A_taken_username_returns_409_on_the_username_field()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (existing, _) = await super.CreateUserAsync();

        var response = await super.PostAsJsonAsync(
            "api/users",
            new CreateUserRequest { FullName = "Copy", Username = existing.Username.ToUpperInvariant(), Password = "Welcome#123" },
            Ct);
        var problem = await ProblemResponse.ReadAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ErrorCodes.UsernameTaken, problem.Code);
        Assert.Equal([ErrorCodes.UsernameTaken], problem.Errors["Username"]);
    }

    /// <summary>
    /// Forces the unique-index path (no Exists check): SaveChanges must map PostgreSQL 23505 to 409 taken,
    /// not a 500. UserService remaps that to username_taken on the HTTP create race below.
    /// </summary>
    [Fact]
    public async Task A_duplicate_username_unique_index_maps_to_409_taken()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (existing, _) = await super.CreateUserAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new User(existing.Username, "Race Other", "race other", "not-a-real-hash", UserRole.Admin));

        var conflict = await Assert.ThrowsAsync<AppException>(() => db.SaveChangesAsync(Ct));

        Assert.Equal(AppErrorKind.Conflict, conflict.Kind);
        Assert.Equal(ErrorCodes.Taken, conflict.Code);
    }

    [Fact]
    public async Task Concurrent_creates_of_the_same_username_return_201_and_409_not_500()
    {
        var a = await factory.CreateClientAsAsync(TestUsers.Super);
        var b = await factory.CreateClientAsAsync(TestUsers.Super);
        var username = $"{HttpClientExtensions.Unique("race")}@test.local";
        var request = new CreateUserRequest
        {
            FullName = "Race User",
            Username = username,
            Password = "Welcome#123",
            Role = Roles.Admin,
        };

        var responses = await Task.WhenAll(
            a.PostAsJsonAsync("api/users", request, Ct),
            b.PostAsJsonAsync("api/users", request, Ct));

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);
        var loser = Assert.Single(responses, r => r.StatusCode != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        var problem = await ProblemResponse.ReadAsync(loser);
        Assert.Equal(ErrorCodes.UsernameTaken, problem.Code);
        Assert.Equal([ErrorCodes.UsernameTaken], problem.Errors["Username"]);
    }

    [Fact]
    public async Task Without_a_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("api/users", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "api/users")]
    [InlineData("GET", "api/users/{id}")]
    [InlineData("POST", "api/users")]
    [InlineData("PUT", "api/users/{id}")]
    [InlineData("POST", "api/users/{id}/reset-password")]
    [InlineData("DELETE", "api/users/{id}")]
    public async Task Admin_gets_403_on_every_user_endpoint(string method, string url)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var request = new HttpRequestMessage(new HttpMethod(method), url.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = method is "POST" or "PUT" ? JsonContent.Create(new { }) : null,
        };

        var response = await admin.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_user_returns_404_with_the_not_found_code()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var problem = await ProblemResponse.ReadAsync(await super.GetAsync($"api/users/{Guid.NewGuid()}", Ct));

        Assert.Equal(HttpStatusCode.NotFound, problem.Status);
        Assert.Equal(ErrorCodes.NotFound, problem.Code);
    }

    [Fact]
    public async Task Update_changes_name_role_and_status_and_records_who_did_it()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, _) = await super.CreateUserAsync();

        var response = await super.PutAsJsonAsync(
            $"api/users/{user.Id}",
            new UpdateUserRequest { FullName = "Renamed", Role = Roles.Super, IsActive = false, Version = user.Version },
            Ct);
        var updated = await response.Content.ReadFromJsonAsync<UserDetailsDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Renamed", updated!.FullName);
        Assert.Equal(Roles.Super, updated.Role);
        Assert.False(updated.IsActive);
        Assert.Equal(user.Version + 1, updated.Version);
        Assert.Equal(TestUsers.Super.Username, updated.UpdatedBy);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Equal(user.CreatedAt, updated.CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Saving_with_a_stale_version_returns_409_concurrency_conflict()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, _) = await super.CreateUserAsync();
        var first = await super.PutAsJsonAsync(
            $"api/users/{user.Id}", new UpdateUserRequest { FullName = "First edit", Version = user.Version }, Ct);

        var second = await super.PutAsJsonAsync(
            $"api/users/{user.Id}", new UpdateUserRequest { FullName = "Second edit", Version = user.Version }, Ct);
        var problem = await ProblemResponse.ReadAsync(second);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, problem.Code);
        Assert.Equal("First edit", (await super.GetJsonAsync<UserDetailsDto>($"api/users/{user.Id}")).FullName);
    }

    [Fact]
    public async Task Super_cannot_deactivate_demote_reset_or_delete_their_own_account()
    {
        var (me, meLogin) = await (await factory.CreateClientAsAsync(TestUsers.Super)).CreateUserAsync(Roles.Super);
        var super = await factory.CreateClientAsAsync(meLogin);

        var responses = new[]
        {
            await super.PutAsJsonAsync($"api/users/{me.Id}", new UpdateUserRequest { FullName = "Me", Role = Roles.Super, IsActive = false, Version = me.Version }, Ct),
            await super.PutAsJsonAsync($"api/users/{me.Id}", new UpdateUserRequest { FullName = "Me", Role = Roles.Admin, Version = me.Version }, Ct),
            await super.PostAsJsonAsync($"api/users/{me.Id}/reset-password", new ResetPasswordRequest { NewPassword = "Another#Pass1" }, Ct),
            await super.DeleteAsync($"api/users/{me.Id}", Ct),
        };

        foreach (var response in responses)
        {
            var problem = await ProblemResponse.ReadAsync(response);
            Assert.Equal(HttpStatusCode.Conflict, problem.Status);
            Assert.Equal(ErrorCodes.OwnAccount, problem.Code);
        }

        var rename = await super.PutAsJsonAsync($"api/users/{me.Id}", new UpdateUserRequest { FullName = "Me renamed", Role = Roles.Super, Version = me.Version }, Ct);
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_user_cannot_sign_in_and_their_token_stops_working()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, login) = await super.CreateUserAsync();
        var userClient = await factory.CreateClientAsAsync(login);

        await super.PutAsJsonAsync($"api/users/{user.Id}", new UpdateUserRequest { FullName = user.FullName, IsActive = false, Version = user.Version }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("api/clinic-settings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().LoginAsync(login.Username, login.Password)).StatusCode);
    }

    [Fact]
    public async Task A_role_change_stops_the_old_token()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, login) = await super.CreateUserAsync(Roles.Super);
        var oldToken = await factory.CreateClientAsAsync(login);

        await super.PutAsJsonAsync($"api/users/{user.Id}", new UpdateUserRequest { FullName = user.FullName, Role = Roles.Admin, Version = user.Version }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, (await oldToken.GetAsync("api/users", Ct)).StatusCode);
        var newLogin = await factory.CreateClient().LoginAsAsync(login with { Role = Roles.Admin });
        Assert.Equal(Roles.Admin, newLogin.User.Role);
    }

    [Fact]
    public async Task Delete_hides_the_user_blocks_sign_in_and_keeps_the_username_reserved()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, login) = await super.CreateUserAsync(fullName: "To Delete");

        var deleted = await super.DeleteAsync($"api/users/{user.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await super.GetAsync($"api/users/{user.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await super.DeleteAsync($"api/users/{user.Id}", Ct)).StatusCode);
        Assert.DoesNotContain(
            (await super.GetJsonAsync<PagedResult<UserListItemDto>>($"api/users?search={user.Username}")).Items, u => u.Id == user.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().LoginAsync(login.Username, login.Password)).StatusCode);

        var again = await super.PostAsJsonAsync(
            "api/users", new CreateUserRequest { FullName = "Again", Username = user.Username, Password = "Welcome#123" }, Ct);
        Assert.Equal(ErrorCodes.UsernameTaken, (await ProblemResponse.ReadAsync(again)).Code);

        await using var scope = factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == user.Id, Ct);
        Assert.True(row.IsDeleted);
        Assert.Equal(TestUsers.Super.Username, row.DeletedBy);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task Reset_password_replaces_the_old_one()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, login) = await super.CreateUserAsync();
        var oldToken = await factory.CreateClientAsAsync(login);

        var response = await super.PostAsJsonAsync(
            $"api/users/{user.Id}/reset-password", new ResetPasswordRequest { NewPassword = "Fresh#Pass99" }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldToken.GetAsync("api/clinic-settings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().LoginAsync(login.Username, login.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().LoginAsync(login.Username, "Fresh#Pass99")).StatusCode);
    }

    [Fact]
    public async Task Reset_password_with_a_short_password_returns_400()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, _) = await super.CreateUserAsync();

        var problem = await ProblemResponse.ReadAsync(await super.PostAsJsonAsync(
            $"api/users/{user.Id}/reset-password", new ResetPasswordRequest { NewPassword = "x" }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([$"min_length|{UserRules.PasswordMinLength}"], problem.Errors["NewPassword"]);
    }

    [Theory]
    [InlineData("احمد فاطمه")]
    [InlineData("أحمد  فاطِمة")]
    [InlineData("آحمد")]
    public async Task Search_finds_arabic_names_however_they_were_typed(string term)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, _) = await super.CreateUserAsync(fullName: "أحمد فاطمة");

        var page = await super.GetJsonAsync<PagedResult<UserListItemDto>>(
            $"api/users?search={Uri.EscapeDataString(term)}&pageSize=100");

        Assert.Contains(page.Items, u => u.Id == user.Id);
    }

    [Fact]
    public async Task Search_also_matches_the_username_and_the_list_is_paged()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, _) = await super.CreateUserAsync();

        var byUsername = await super.GetJsonAsync<PagedResult<UserListItemDto>>($"api/users?search={user.Username.ToUpperInvariant()}");
        var firstPage = await super.GetJsonAsync<PagedResult<UserListItemDto>>("api/users?page=1&pageSize=1");
        var withoutTotal = await super.GetJsonAsync<PagedResult<UserListItemDto>>("api/users?page=1&pageSize=1&includeTotal=false");

        Assert.Equal(user.Id, Assert.Single(byUsername.Items).Id);
        Assert.Equal(user.Version, byUsername.Items[0].Version);
        Assert.Single(firstPage.Items);
        Assert.True(firstPage.TotalCount >= 3);
        Assert.Equal(PageQuery.TotalNotComputed, withoutTotal.TotalCount);
        Assert.DoesNotContain("passwordHash", await (await super.GetAsync("api/users?page=1&pageSize=1", Ct)).Content.ReadAsStringAsync(Ct), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task User_management_actions_are_recorded_in_the_activity_log()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (user, _) = await super.CreateUserAsync();
        await super.PutAsJsonAsync($"api/users/{user.Id}", new UpdateUserRequest { FullName = "Edited", Version = user.Version }, Ct);
        await super.PostAsJsonAsync($"api/users/{user.Id}/reset-password", new ResetPasswordRequest { NewPassword = "Fresh#Pass99" }, Ct);
        await super.DeleteAsync($"api/users/{user.Id}", Ct);

        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=users.&pageSize=100");
        var actions = new List<string>();
        foreach (var item in page.Items)
        {
            var details = await super.GetJsonAsync<ActivityLogDto>($"api/activity-logs/{item.Id}");
            if (details.EntityId == user.Id.ToString())
            {
                actions.Add(details.Action);
                Assert.DoesNotContain("Fresh#Pass99", details.Details ?? string.Empty);
            }
        }

        Assert.Equal(["users.created", "users.deleted", "users.password_reset", "users.updated"], actions.Order().ToArray());
    }
}

using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;

namespace Mostawsef.Api.IntegrationTests.LabTests;

[Collection(ApiCollection.Name)]
public class LabTestsTests(ApiFactory factory)
{
    private const string Url = "api/lab-tests";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_creates_a_test_and_reads_it_back()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var code = UniqueCode();

        var response = await admin.PostAsJsonAsync(
            Url,
            new CreateLabTestRequest { Code = $"  {code.ToLowerInvariant()} ", Name = $"  Complete blood count {code} ", Price = 15050, LabCost = 9000 },
            Ct);
        var created = await response.Content.ReadFromJsonAsync<LabTestDto>(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"{Url}/{created!.Id}", response.Headers.Location!.ToString());
        Assert.Equal(code, created.Code);
        Assert.Equal($"Complete blood count {code}", created.Name);
        Assert.Equal((15050, 9000), (created.Price, created.LabCost));
        Assert.True(created.IsActive);
        Assert.Equal(0, created.Version);
        var reread = await admin.GetJsonAsync<LabTestDto>($"{Url}/{created.Id}");
        Assert.Equal(created, reread with { CreatedAt = created.CreatedAt });
        Assert.Equal(created.CreatedAt, reread.CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_an_error_code_per_field()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            Url, new CreateLabTestRequest { Code = new string('X', 21), Name = " ", Price = -1 }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(ErrorCodes.Validation, problem.Code);
        Assert.Equal([$"max_length|{LabTestRules.CodeMaxLength}"], problem.Errors["Code"]);
        Assert.Equal([ErrorCodes.Required], problem.Errors["Name"]);
        Assert.Equal([$"range|0|{LabTestRules.MaxAmount}"], problem.Errors["Price"]);
        Assert.Equal([ErrorCodes.Required], problem.Errors["LabCost"]);
    }

    [Fact]
    public async Task Prices_above_the_limit_and_too_long_names_are_rejected_on_update_too()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync($"{Url}/{test.Id}", new UpdateLabTestRequest
        {
            Code = test.Code, Name = new string('x', 151), Price = LabTestRules.MaxAmount + 1, LabCost = LabTestRules.MaxAmount, Version = test.Version,
        }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([$"max_length|{LabTestRules.NameMaxLength}"], problem.Errors["Name"]);
        Assert.Equal([$"range|0|{LabTestRules.MaxAmount}"], problem.Errors["Price"]);
        Assert.False(problem.Errors.ContainsKey("LabCost"));
    }

    [Fact]
    public async Task A_very_long_search_is_cut_instead_of_rejected()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var name = $"{Guid.NewGuid():N}".PadRight(PageQuery.MaxSearchLength, 'q');
        var test = await CreateAsync(admin, name);

        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={name}-not-in-the-name");

        Assert.Equal(test.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task A_code_or_name_already_used_returns_409_on_that_field_however_it_is_typed()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var existing = await CreateAsync(admin, name: $"تحليل السكر {Guid.NewGuid():N}");

        var sameCode = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            Url, new CreateLabTestRequest { Code = existing.Code.ToLowerInvariant(), Name = "Another name", Price = 1, LabCost = 1 }, Ct));
        var sameName = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            Url, new CreateLabTestRequest { Code = UniqueCode(), Name = existing.Name.Replace("السكر", "الس\u0651كر"), Price = 1, LabCost = 1 }, Ct));

        Assert.Equal(HttpStatusCode.Conflict, sameCode.Status);
        Assert.Equal([ErrorCodes.Taken], sameCode.Errors["Code"]);
        Assert.Equal(HttpStatusCode.Conflict, sameName.Status);
        Assert.Equal([ErrorCodes.Taken], sameName.Errors["Name"]);
    }

    [Fact]
    public async Task Without_a_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync(Url, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", Url)]
    [InlineData("GET", Url + "/{id}")]
    [InlineData("POST", Url)]
    [InlineData("PUT", Url + "/{id}")]
    [InlineData("DELETE", Url + "/{id}")]
    public async Task Super_gets_403_on_every_lab_test_endpoint(string method, string url)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var request = new HttpRequestMessage(new HttpMethod(method), url.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = method is "POST" or "PUT" ? JsonContent.Create(new { }) : null,
        };

        var response = await super.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_test_returns_404_for_read_update_and_delete()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var url = $"{Url}/{Guid.NewGuid()}";

        var responses = new[]
        {
            await admin.GetAsync(url, Ct),
            await admin.PutAsJsonAsync(url, new UpdateLabTestRequest { Code = "X", Name = "X", Price = 1, LabCost = 1 }, Ct),
            await admin.DeleteAsync(url, Ct),
        };

        foreach (var response in responses)
        {
            var problem = await ProblemResponse.ReadAsync(response);
            Assert.Equal(HttpStatusCode.NotFound, problem.Status);
            Assert.Equal(ErrorCodes.NotFound, problem.Code);
        }
    }

    [Fact]
    public async Task Update_changes_every_field_and_records_who_did_it()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);
        var newCode = UniqueCode();

        var response = await admin.PutAsJsonAsync($"{Url}/{test.Id}", new UpdateLabTestRequest
        {
            Code = newCode, Name = test.Name + " (fasting)", Price = 2500, LabCost = 1200, IsActive = false, Version = test.Version,
        }, Ct);
        var updated = await response.Content.ReadFromJsonAsync<LabTestDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((newCode, test.Name + " (fasting)", 2500L, 1200L, false), (updated!.Code, updated.Name, updated.Price, updated.LabCost, updated.IsActive));
        Assert.Equal(test.Version + 1, updated.Version);
        Assert.Equal(TestUsers.Admin.Username, updated.UpdatedBy);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Saving_a_test_with_its_own_code_and_name_is_not_a_conflict()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);

        var response = await admin.PutAsJsonAsync($"{Url}/{test.Id}", new UpdateLabTestRequest
        {
            Code = test.Code, Name = test.Name, Price = test.Price + 100, LabCost = test.LabCost, Version = test.Version,
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Saving_with_a_stale_version_returns_409_concurrency_conflict()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);
        var request = new UpdateLabTestRequest { Code = test.Code, Name = test.Name, Price = 1, LabCost = 1, Version = test.Version };
        (await admin.PutAsJsonAsync($"{Url}/{test.Id}", request, Ct)).EnsureSuccessStatusCode();

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync($"{Url}/{test.Id}", request with { Price = 2 }, Ct));

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, problem.Code);
    }

    [Fact]
    public async Task Delete_removes_the_test_and_frees_its_code()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);

        var deleted = await admin.DeleteAsync($"{Url}/{test.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{test.Id}", Ct)).StatusCode);
        var again = await admin.PostAsJsonAsync(Url, new CreateLabTestRequest { Code = test.Code, Name = test.Name, Price = 1, LabCost = 1 }, Ct);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task A_test_used_by_an_order_cannot_be_deleted()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);
        var order = await admin.PostAsJsonAsync("api/orders", new CreateOrderRequest
        {
            NewPatient = new() { FullName = "Lab Test User", Phone = "0100", Gender = Genders.Male },
            Items = [new() { LabTestId = test.Id }],
        }, Ct);
        order.EnsureSuccessStatusCode();

        var problem = await ProblemResponse.ReadAsync(await admin.DeleteAsync($"{Url}/{test.Id}", Ct));

        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.InUse), (problem.Status, problem.Code));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{Url}/{test.Id}", Ct)).StatusCode);
    }

    [Theory]
    [InlineData("فيتامين د")]
    [InlineData("فيتامين  دِ")]
    public async Task Search_finds_arabic_names_however_they_were_typed(string term)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin, name: $"فيتامين د {Guid.NewGuid():N}");

        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(term)}&pageSize=100");

        Assert.Contains(page.Items, t => t.Id == test.Id);
    }

    [Fact]
    public async Task Search_also_matches_the_code_and_the_list_is_paged()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);
        await CreateAsync(admin);

        var byCode = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={test.Code.ToLowerInvariant()}");
        var firstPage = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?page=1&pageSize=1");

        Assert.Equal(test.Id, Assert.Single(byCode.Items).Id);
        Assert.Single(firstPage.Items);
        Assert.True(firstPage.TotalCount >= 2);
    }

    [Fact]
    public async Task Lab_test_actions_are_recorded_in_the_activity_log()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);
        await admin.PutAsJsonAsync($"{Url}/{test.Id}", new UpdateLabTestRequest
        {
            Code = test.Code, Name = test.Name, Price = 1, LabCost = 1, Version = test.Version,
        }, Ct);
        await admin.DeleteAsync($"{Url}/{test.Id}", Ct);

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=lab_tests.&pageSize=100");
        var actions = new List<string>();
        foreach (var item in page.Items)
        {
            var details = await super.GetJsonAsync<ActivityLogDto>($"api/activity-logs/{item.Id}");
            if (details.EntityId == test.Id.ToString())
            {
                actions.Add(details.Action);
                Assert.Equal(TestUsers.Admin.Username, details.Username);
            }
        }

        Assert.Equal(["lab_tests.created", "lab_tests.deleted", "lab_tests.updated"], actions.Order().ToArray());
    }

    private static string UniqueCode() => $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private static async Task<LabTestDto> CreateAsync(HttpClient admin, string? name = null)
    {
        var code = UniqueCode();
        var response = await admin.PostAsJsonAsync(
            Url, new CreateLabTestRequest { Code = code, Name = name ?? $"Test {Guid.NewGuid():N}", Price = 10000, LabCost = 6000 }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LabTestDto>(Ct))!;
    }
}

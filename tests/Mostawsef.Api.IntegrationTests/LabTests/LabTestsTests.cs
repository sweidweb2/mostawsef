using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
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
            new CreateLabTestRequest { Code = $"  {code.ToLowerInvariant()} ", Name = $"  Complete blood count {code} ", LabCost = 9000 },
            Ct);
        var created = await response.Content.ReadFromJsonAsync<LabTestDto>(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"{Url}/{created!.Id}", response.Headers.Location!.ToString());
        Assert.Equal(code, created.Code);
        Assert.Equal($"Complete blood count {code}", created.Name);
        Assert.Equal(9000, created.LabCost);
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
            Url, new CreateLabTestRequest { Code = new string('X', 21), Name = " ", LabCost = -1 }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(ErrorCodes.Validation, problem.Code);
        Assert.Equal([$"max_length|{LabTestRules.CodeMaxLength}"], problem.Errors["Code"]);
        Assert.Equal([ErrorCodes.Required], problem.Errors["Name"]);
        Assert.Equal([$"range|0|{LabTestRules.MaxAmount}"], problem.Errors["LabCost"]);
    }

    [Fact]
    public async Task Lab_costs_above_the_limit_and_too_long_names_are_rejected_on_update_too()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync($"{Url}/{test.Id}", new UpdateLabTestRequest
        {
            Code = test.Code, Name = new string('x', 151), LabCost = LabTestRules.MaxAmount + 1, Version = test.Version,
        }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([$"max_length|{LabTestRules.NameMaxLength}"], problem.Errors["Name"]);
        Assert.Equal([$"range|0|{LabTestRules.MaxAmount}"], problem.Errors["LabCost"]);
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
            Url, new CreateLabTestRequest { Code = existing.Code.ToLowerInvariant(), Name = "Another name", LabCost = 1 }, Ct));
        var sameName = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            Url, new CreateLabTestRequest { Code = UniqueCode(), Name = existing.Name.Replace("السكر", "الس\u0651كر"), LabCost = 1 }, Ct));

        Assert.Equal(HttpStatusCode.Conflict, sameCode.Status);
        Assert.Equal([ErrorCodes.Taken], sameCode.Errors["Code"]);
        Assert.Equal(HttpStatusCode.Conflict, sameName.Status);
        Assert.Equal([ErrorCodes.Taken], sameName.Errors["Name"]);
    }

    [Fact]
    public async Task Concurrent_creates_of_the_same_code_return_201_and_409_not_500()
    {
        var a = await factory.CreateClientAsAsync(TestUsers.Admin);
        var b = await factory.CreateClientAsAsync(TestUsers.Admin);
        var code = UniqueCode();
        var request = new CreateLabTestRequest
        {
            Code = code,
            Name = $"Race {Guid.NewGuid():N}",
            LabCost = 1,
        };

        var responses = await Task.WhenAll(
            a.PostAsJsonAsync(Url, request, Ct),
            b.PostAsJsonAsync(Url, request, Ct));

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);
        var loser = Assert.Single(responses, r => r.StatusCode != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        var problem = await ProblemResponse.ReadAsync(loser);
        Assert.Equal(ErrorCodes.Taken, problem.Code);
        Assert.Equal([ErrorCodes.Taken], problem.Errors["Code"]);
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
    [InlineData("POST", Url + "/import")]
    [InlineData("PUT", Url + "/{id}")]
    [InlineData("DELETE", Url + "/{id}")]
    public async Task Super_gets_403_on_every_lab_test_endpoint(string method, string url)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var request = new HttpRequestMessage(new HttpMethod(method), url.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = method is "POST" or "PUT"
                ? (url.EndsWith("/import", StringComparison.Ordinal)
                    ? ImportFile("Name,Price\r\nA,$1.00\r\n")
                    : JsonContent.Create(new { }))
                : null,
        };

        var response = await super.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Import_creates_tests_from_csv_with_dollar_prices_and_generates_codes()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var name = $"Import CBC {Guid.NewGuid():N}";

        var response = await admin.PostAsync($"{Url}/import", ImportFile($"Name,Price\r\n{name},$0.08\r\n"), Ct);
        var result = await response.Content.ReadFromJsonAsync<ImportLabTestsResult>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((1, 0, 0), (result!.Imported, result.Updated, result.Skipped));
        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(name)}");
        var created = Assert.Single(page.Items);
        Assert.Equal(8, created.LabCost);
        Assert.Equal(8, created.Code.Length);
        Assert.Matches("^[A-Z0-9]+$", created.Code);
    }

    [Fact]
    public async Task Import_updates_existing_names_skips_bad_and_duplicate_in_file_rows_and_stops_after_ten_empty_rows()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var existing = await CreateAsync(admin, name: $"Already {Guid.NewGuid():N}");
        var good = $"Good {Guid.NewGuid():N}";
        var duplicateInFile = $"Dup {Guid.NewGuid():N}";
        var afterGap = $"After gap {Guid.NewGuid():N}";
        var overMax = $"Over max {Guid.NewGuid():N}";
        var shouldNot = $"Should not import {Guid.NewGuid():N}";
        var csv =
            "Name,Price\r\n" +
            $"{good},$2.00\r\n" +
            $"{duplicateInFile},$1.00\r\n" +
            $"{duplicateInFile},$3.00\r\n" +
            $"{existing.Name},$4.00\r\n" +
            ",$5.00\r\n" +
            "Missing price,\r\n" +
            $"Bad price {Guid.NewGuid():N},not-a-price\r\n" +
            // Quoted so the CSV commas in a thousands-style over-max price stay in the Price cell.
            $"{overMax},\"$9,999,999.00\"\r\n" +
            $"{afterGap},$1.24\r\n" +
            string.Concat(Enumerable.Repeat("\r\n", LabTestRules.ImportEmptyStopRows)) +
            $"{shouldNot},$9.00\r\n";

        var response = await admin.PostAsync($"{Url}/import", ImportFile(csv), Ct);
        var result = await response.Content.ReadFromJsonAsync<ImportLabTestsResult>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // good + first Dup + afterGap created; existing name updated; second Dup / bad / over-max skipped.
        Assert.Equal((3, 1), (result!.Imported, result.Updated));
        Assert.True(result.Skipped >= 5);
        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(good)}&pageSize=100");
        Assert.Contains(page.Items, t => t.Name == good && t.LabCost == 200);
        page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(duplicateInFile)}&pageSize=100");
        Assert.Contains(page.Items, t => t.Name == duplicateInFile && t.LabCost == 100);
        page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(afterGap)}&pageSize=100");
        Assert.Contains(page.Items, t => t.Name == afterGap && t.LabCost == 124);
        page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(existing.Name)}&pageSize=100");
        var overridden = Assert.Single(page.Items, t => t.Id == existing.Id);
        Assert.Equal((existing.Code, 400), (overridden.Code, overridden.LabCost));
        Assert.DoesNotContain(
            (await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(shouldNot)}&pageSize=100")).Items,
            t => t.Name == shouldNot);
        Assert.DoesNotContain(
            (await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(overMax)}&pageSize=100")).Items,
            t => t.Name == overMax);
    }

    [Fact]
    public async Task Import_from_csv_template_round_trips_the_sample_row()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.PostAsync($"{Url}/import", ImportFile(LabTestImportCsv.Content, LabTestImportCsv.FileName), Ct);
        var result = await response.Content.ReadFromJsonAsync<ImportLabTestsResult>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, result!.Imported);
        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search=Complete%20blood%20count&pageSize=100");
        Assert.Contains(page.Items, t => t.Name == "Complete blood count" && t.LabCost == 1200);
    }

    [Fact]
    public async Task Import_keeps_a_first_data_row_named_Test()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        // Exact name "Test" is shared across runs; clear leftovers so uniqueness does not mask the header bug.
        var leftovers = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search=Test&pageSize=100");
        foreach (var row in leftovers.Items.Where(t => t.Name == "Test"))
        {
            (await admin.DeleteAsync($"{Url}/{row.Id}", Ct)).EnsureSuccessStatusCode();
        }

        var other = $"Other {Guid.NewGuid():N}";
        // "Test" alone used to match LooksLikeNameHeader and was dropped silently.
        var csv = $"Name,Price\r\nTest,$1.50\r\n{other},$2.00\r\n";

        var response = await admin.PostAsync($"{Url}/import", ImportFile(csv), Ct);
        var result = await response.Content.ReadFromJsonAsync<ImportLabTestsResult>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, result!.Imported);
        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(other)}&pageSize=100");
        Assert.Contains(page.Items, t => t.Name == other && t.LabCost == 200);
        page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search=Test&pageSize=100");
        Assert.Contains(page.Items, t => t.Name == "Test" && t.LabCost == 150);
    }

    [Fact]
    public async Task Import_parses_quoted_us_thousands_prices()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var us = $"US {Guid.NewGuid():N}";
        var csv = $"Name,Price\r\n\"{us}\",\"$1,234.50\"\r\n";

        var response = await admin.PostAsync($"{Url}/import", ImportFile(csv), Ct);
        var result = await response.Content.ReadFromJsonAsync<ImportLabTestsResult>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((1, 0, 0), (result!.Imported, result.Updated, result.Skipped));
        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(us)}");
        Assert.Equal(123_450, Assert.Single(page.Items).LabCost);
    }

    [Fact]
    public async Task Concurrent_imports_of_the_same_name_do_not_500()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var name = $"Race {Guid.NewGuid():N}";
        var csv = $"Name,Price\r\n{name},$3.00\r\n";

        var firstTask = admin.PostAsync($"{Url}/import", ImportFile(csv), Ct);
        var secondTask = admin.PostAsync($"{Url}/import", ImportFile(csv), Ct);
        var responses = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
        Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
        var results = new[]
        {
            await responses[0].Content.ReadFromJsonAsync<ImportLabTestsResult>(Ct),
            await responses[1].Content.ReadFromJsonAsync<ImportLabTestsResult>(Ct),
        };
        Assert.Equal(1, results.Sum(r => r!.Imported));
        Assert.Equal(1, results.Sum(r => r!.Updated));
        Assert.Equal(0, results.Sum(r => r!.Skipped));
        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(name)}");
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Import_creates_tests_from_xlsx_with_dollar_and_numeric_prices()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var dollar = $"Xlsx dollar {Guid.NewGuid():N}";
        var numeric = $"Xlsx numeric {Guid.NewGuid():N}";
        using var form = ImportXlsxFile(
            ("Name", "Price"),
            (dollar, "$0.08"),
            (numeric, 2.5));

        var response = await admin.PostAsync($"{Url}/import", form, Ct);
        var result = await response.Content.ReadFromJsonAsync<ImportLabTestsResult>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((2, 0, 0), (result!.Imported, result.Updated, result.Skipped));
        var page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(dollar)}");
        Assert.Equal(8, Assert.Single(page.Items).LabCost);
        page = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?search={Uri.EscapeDataString(numeric)}");
        Assert.Equal(250, Assert.Single(page.Items).LabCost);
    }

    [Fact]
    public async Task Import_rejects_an_unsupported_file_type()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("not a spreadsheet"u8.ToArray()), "file", "tests.pdf");

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsync($"{Url}/import", form, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(ErrorCodes.Validation, problem.Code);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["File"]);
    }

    [Fact]
    public async Task Import_rejects_a_corrupt_xlsx_file()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("not xlsx"u8.ToArray()), "file", "tests.xlsx");

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsync($"{Url}/import", form, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(ErrorCodes.Validation, problem.Code);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["File"]);
    }

    [Fact]
    public async Task Import_without_a_file_returns_400()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([]), "file", "empty.csv");

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsync($"{Url}/import", form, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(ErrorCodes.Validation, problem.Code);
        Assert.Equal([ErrorCodes.Required], problem.Errors["File"]);
    }

    [Fact]
    public async Task Import_is_recorded_in_the_activity_log()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var name = $"Logged import {Guid.NewGuid():N}";
        (await admin.PostAsync($"{Url}/import", ImportFile($"Name,Price\r\n{name},$1.00\r\n"), Ct)).EnsureSuccessStatusCode();

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=lab_tests.imported&pageSize=50");
        Assert.Contains(page.Items, i => i.Action == "lab_tests.imported");
    }

    [Fact]
    public async Task Unknown_test_returns_404_for_read_update_and_delete()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var url = $"{Url}/{Guid.NewGuid()}";

        var responses = new[]
        {
            await admin.GetAsync(url, Ct),
            await admin.PutAsJsonAsync(url, new UpdateLabTestRequest { Code = "X", Name = "X", LabCost = 1 }, Ct),
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
            Code = newCode, Name = test.Name + " (fasting)", LabCost = 1200, IsActive = false, Version = test.Version,
        }, Ct);
        var updated = await response.Content.ReadFromJsonAsync<LabTestDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((newCode, test.Name + " (fasting)", 1200L, false), (updated!.Code, updated.Name, updated.LabCost, updated.IsActive));
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
            Code = test.Code, Name = test.Name, LabCost = test.LabCost + 100, Version = test.Version,
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Saving_with_a_stale_version_returns_409_concurrency_conflict()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);
        var request = new UpdateLabTestRequest { Code = test.Code, Name = test.Name, LabCost = 1, Version = test.Version };
        (await admin.PutAsJsonAsync($"{Url}/{test.Id}", request, Ct)).EnsureSuccessStatusCode();

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync($"{Url}/{test.Id}", request with { LabCost = 2 }, Ct));

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
        var again = await admin.PostAsJsonAsync(Url, new CreateLabTestRequest { Code = test.Code, Name = test.Name, LabCost = 1 }, Ct);
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
        var withoutTotal = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?page=1&pageSize=1&includeTotal=false");

        Assert.Equal(test.Id, Assert.Single(byCode.Items).Id);
        Assert.Single(firstPage.Items);
        Assert.True(firstPage.TotalCount >= 2);
        Assert.Equal(PageQuery.TotalNotComputed, withoutTotal.TotalCount);
    }

    [Fact]
    public async Task Active_filter_hides_inactive_tests()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var active = await CreateAsync(admin);
        var retired = await CreateAsync(admin);
        (await admin.PutAsJsonAsync($"{Url}/{retired.Id}", new UpdateLabTestRequest
        {
            Code = retired.Code, Name = retired.Name, LabCost = retired.LabCost, IsActive = false, Version = retired.Version,
        }, Ct)).EnsureSuccessStatusCode();

        var onlyActive = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?active=true&pageSize=100&search={active.Code}");
        var onlyInactive = await admin.GetJsonAsync<PagedResult<LabTestDto>>($"{Url}?active=false&pageSize=100&search={retired.Code}");

        Assert.Equal(active.Id, Assert.Single(onlyActive.Items).Id);
        Assert.Equal(retired.Id, Assert.Single(onlyInactive.Items).Id);
    }

    [Fact]
    public async Task All_returns_every_test_in_one_response()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var first = await CreateAsync(admin, name: $"Export A {Guid.NewGuid():N}");
        var second = await CreateAsync(admin, name: $"Export B {Guid.NewGuid():N}");

        var all = await admin.GetJsonAsync<IReadOnlyList<LabTestDto>>($"{Url}/all");

        Assert.Contains(all, t => t.Id == first.Id);
        Assert.Contains(all, t => t.Id == second.Id);
    }

    [Fact]
    public async Task All_active_true_returns_only_active_tests()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var active = await CreateAsync(admin, name: $"Catalog A {Guid.NewGuid():N}");
        var retired = await CreateAsync(admin, name: $"Catalog B {Guid.NewGuid():N}");
        (await admin.PutAsJsonAsync($"{Url}/{retired.Id}", new UpdateLabTestRequest
        {
            Code = retired.Code, Name = retired.Name, LabCost = retired.LabCost, IsActive = false, Version = retired.Version,
        }, Ct)).EnsureSuccessStatusCode();

        var catalog = await admin.GetJsonAsync<IReadOnlyList<LabTestDto>>($"{Url}/all?active=true");

        Assert.Contains(catalog, t => t.Id == active.Id);
        Assert.DoesNotContain(catalog, t => t.Id == retired.Id);
    }

    [Fact]
    public async Task Lab_test_actions_are_recorded_in_the_activity_log()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateAsync(admin);
        await admin.PutAsJsonAsync($"{Url}/{test.Id}", new UpdateLabTestRequest
        {
            Code = test.Code, Name = test.Name, LabCost = 1, Version = test.Version,
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

    private static MultipartFormDataContent ImportFile(string csv, string fileName = "tests.csv")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", fileName);
        return form;
    }

    private static MultipartFormDataContent ImportXlsxFile(params (object? Name, object? Price)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Tests");
        for (var i = 0; i < rows.Length; i++)
        {
            SetCell(sheet.Cell(i + 1, 1), rows[i].Name);
            SetCell(sheet.Cell(i + 1, 2), rows[i].Price);
        }

        using var buffer = new MemoryStream();
        workbook.SaveAs(buffer);
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(buffer.ToArray()), "file", "tests.xlsx");
        return form;
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Clear();
                break;
            case string text:
                cell.Value = text;
                break;
            case double number:
                cell.Value = number;
                break;
            case int number:
                cell.Value = number;
                break;
            case decimal number:
                cell.Value = number;
                break;
            default:
                cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture);
                break;
        }
    }

    private static async Task<LabTestDto> CreateAsync(HttpClient admin, string? name = null)
    {
        var code = UniqueCode();
        var response = await admin.PostAsJsonAsync(
            Url, new CreateLabTestRequest { Code = code, Name = name ?? $"Test {Guid.NewGuid():N}", LabCost = 6000 }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LabTestDto>(Ct))!;
    }
}

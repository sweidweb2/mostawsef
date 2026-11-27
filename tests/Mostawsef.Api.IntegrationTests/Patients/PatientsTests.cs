using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Patients;

[Collection(ApiCollection.Name)]
public class PatientsTests(ApiFactory factory)
{
    private const string Url = "api/patients";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_patients_may_share_the_same_phone()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var phone = "01099998888";
        var first = await CreateAsync(admin, fullName: $"First {Guid.NewGuid():N}", phone: phone);
        var second = await CreateAsync(admin, fullName: $"Second {Guid.NewGuid():N}", phone: phone);

        Assert.Equal(phone, first.Phone);
        Assert.Equal(phone, second.Phone);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Admin_creates_a_patient_and_reads_it_back()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var name = $"Mona Adel {Guid.NewGuid():N}";

        var response = await admin.PostAsJsonAsync(Url, new CreatePatientRequest
        {
            FullName = $"  {name} ",
            Phone = " ٠١٠  ١٢٣٤ ٥٦٧٨ ",
            Notes = "  Allergic to penicillin ",
            Gender = Genders.Female,
            DateOfBirth = new DateOnly(2015, 3, 9),
        }, Ct);
        var created = await response.Content.ReadFromJsonAsync<PatientDto>(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"{Url}/{created!.Id}", response.Headers.Location!.ToString());
        Assert.Equal(name, created.FullName);
        Assert.Equal("010 1234 5678", created.Phone);
        Assert.Equal("Allergic to penicillin", created.Notes);
        Assert.Equal((Genders.Female, new DateOnly(2015, 3, 9)), (created.Gender, created.DateOfBirth));
        Assert.Equal(0, created.Version);
        var reread = await admin.GetJsonAsync<PatientDto>($"{Url}/{created.Id}");
        Assert.Equal(created, reread with { CreatedAt = created.CreatedAt });
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_an_error_code_per_field()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, new CreatePatientRequest
        {
            FullName = " ",
            Phone = "call me",
            Notes = new string('n', 2001),
            Gender = "Other",
        }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([ErrorCodes.Required], problem.Errors["FullName"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["Phone"]);
        Assert.Equal([$"max_length|{PatientRules.NotesMaxLength}"], problem.Errors["Notes"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["Gender"]);
    }

    [Theory]
    [InlineData(1899, 12, 31)]
    [InlineData(2999, 1, 1)]
    public async Task A_date_of_birth_before_1900_or_in_the_future_returns_400(int year, int month, int day)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            Url, Valid() with { DateOfBirth = new DateOnly(year, month, day) }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["DateOfBirth"]);
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
    [InlineData("GET", Url + "/{id}/details")]
    [InlineData("POST", Url)]
    [InlineData("PUT", Url + "/{id}")]
    [InlineData("DELETE", Url + "/{id}")]
    public async Task Super_gets_403_on_every_patient_endpoint(string method, string url)
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
    public async Task Unknown_patient_returns_404_for_read_update_and_delete()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var url = $"{Url}/{Guid.NewGuid()}";

        var responses = new[]
        {
            await admin.GetAsync(url, Ct),
            await admin.GetAsync($"{url}/details", Ct),
            await admin.PutAsJsonAsync(url, Update(Valid(), 0), Ct),
            await admin.DeleteAsync(url, Ct),
        };

        foreach (var response in responses)
        {
            Assert.Equal(ErrorCodes.NotFound, (await ProblemResponse.ReadAsync(response)).Code);
        }
    }

    [Fact]
    public async Task Details_returns_the_patient_and_their_newest_orders()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);
        var test = await admin.PostAsJsonAsync("api/lab-tests", new CreateLabTestRequest
        {
            Code = $"P{Guid.NewGuid():N}"[..8], Name = $"Details test {Guid.NewGuid():N}", LabCost = 1000,
        }, Ct);
        test.EnsureSuccessStatusCode();
        var labTest = await test.Content.ReadFromJsonAsync<LabTestDto>(Ct);
        var order = await admin.PostAsJsonAsync("api/orders", new CreateOrderRequest
        {
            PatientId = patient.Id,
            Items = [new() { LabTestId = labTest!.Id }],
        }, Ct);
        order.EnsureSuccessStatusCode();
        var created = await order.Content.ReadFromJsonAsync<OrderDetailsDto>(Ct);

        var details = await admin.GetJsonAsync<PatientDetailsDto>($"{Url}/{patient.Id}/details");

        Assert.Equal(patient.Id, details.Patient.Id);
        Assert.Equal(created!.Id, Assert.Single(details.Orders).Id);
    }

    [Fact]
    public async Task Update_changes_every_field_and_records_who_did_it()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);

        var response = await admin.PutAsJsonAsync($"{Url}/{patient.Id}", new UpdatePatientRequest
        {
            FullName = "Renamed", Phone = "0111", Notes = "New note",
            Gender = Genders.Female, DateOfBirth = new DateOnly(1990, 1, 1), Version = patient.Version,
        }, Ct);
        var updated = await response.Content.ReadFromJsonAsync<PatientDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ("Renamed", "0111", "New note", Genders.Female, (DateOnly?)new DateOnly(1990, 1, 1)),
            (updated!.FullName, updated.Phone, updated.Notes, updated.Gender, updated.DateOfBirth));
        Assert.Equal(patient.Version + 1, updated.Version);
        Assert.Equal(TestUsers.Admin.Username, updated.UpdatedBy);
    }

    [Fact]
    public async Task Saving_with_a_stale_version_returns_409_concurrency_conflict()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);
        (await admin.PutAsJsonAsync($"{Url}/{patient.Id}", Update(Valid(), patient.Version), Ct)).EnsureSuccessStatusCode();

        var problem = await ProblemResponse.ReadAsync(await admin.PutAsJsonAsync($"{Url}/{patient.Id}", Update(Valid(), patient.Version), Ct));

        Assert.Equal(HttpStatusCode.Conflict, problem.Status);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, problem.Code);
    }

    [Fact]
    public async Task Delete_hides_the_patient_but_keeps_the_row()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);

        var deleted = await admin.DeleteAsync($"{Url}/{patient.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Url}/{patient.Id}", Ct)).StatusCode);
        Assert.DoesNotContain(
            (await admin.GetJsonAsync<PagedResult<PatientDto>>($"{Url}?search={Uri.EscapeDataString(patient.FullName)}")).Items,
            p => p.Id == patient.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Patients
            .IgnoreQueryFilters().AsNoTracking().SingleAsync(p => p.Id == patient.Id, Ct);
        Assert.True(row.IsDeleted);
        Assert.Equal(TestUsers.Admin.Username, row.DeletedBy);
    }

    [Theory]
    [InlineData("فاطمة")]
    [InlineData("فاطِمه")]
    public async Task Search_finds_arabic_names_however_they_were_typed(string term)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin, $"فاطمة {Guid.NewGuid():N}");

        var page = await admin.GetJsonAsync<PagedResult<PatientDto>>($"{Url}?search={Uri.EscapeDataString(term)}&pageSize=100");

        Assert.Contains(page.Items, p => p.Id == patient.Id);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a b")]
    [InlineData("ـمـ")]
    public async Task Search_with_fewer_than_3_letters_or_digits_returns_400_min_length(string search)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.GetAsync($"{Url}?search={Uri.EscapeDataString(search)}", Ct);

        var problem = await ProblemResponse.ReadAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(["min_length|3"], problem.Errors["Search"]);
    }

    [Fact]
    public async Task Search_matches_the_phone_in_any_digits()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var digits = Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var byPhone = await CreateAsync(admin, phone: "01" + digits);
        var arabicDigits = string.Concat(digits.Select(d => (char)('\u0660' + (d - '0'))));

        var page = await admin.GetJsonAsync<PagedResult<PatientDto>>($"{Url}?search={Uri.EscapeDataString(arabicDigits)}");

        Assert.Contains(page.Items, p => p.Id == byPhone.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var searchText = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Patients
            .Where(p => p.Id == byPhone.Id).Select(p => p.SearchText).SingleAsync(Ct);
        Assert.Contains(SearchText.Normalize(byPhone.Phone), searchText);
    }

    [Fact]
    public async Task The_list_omits_notes_and_skips_the_total_when_asked()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var created = await CreateAsync(admin);
        await admin.PutAsJsonAsync($"{Url}/{created.Id}", Update(Valid() with
        {
            FullName = created.FullName, Phone = created.Phone, Notes = "Secret medical note",
        }, created.Version), Ct);

        var listed = (await admin.GetJsonAsync<PagedResult<PatientDto>>($"{Url}?search={Uri.EscapeDataString(created.FullName)}")).Items.Single();
        var withoutTotal = await admin.GetJsonAsync<PagedResult<PatientDto>>(
            $"{Url}?search={Uri.EscapeDataString(created.FullName)}&includeTotal=false");
        var details = await admin.GetJsonAsync<PatientDto>($"{Url}/{created.Id}");

        Assert.Null(listed.Notes);
        Assert.Equal(PageQuery.TotalNotComputed, withoutTotal.TotalCount);
        Assert.Equal("Secret medical note", details.Notes);
    }

    [Fact]
    public async Task All_returns_every_patient_without_notes_in_one_response()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var first = await CreateAsync(admin, $"Export A {Guid.NewGuid():N}");
        var second = await CreateAsync(admin, $"Export B {Guid.NewGuid():N}");
        await admin.PutAsJsonAsync($"{Url}/{first.Id}", Update(Valid() with
        {
            FullName = first.FullName, Phone = first.Phone, Notes = "Hidden",
        }, first.Version), Ct);

        var all = await admin.GetJsonAsync<IReadOnlyList<PatientDto>>($"{Url}/all");

        Assert.Contains(all, p => p.Id == first.Id && p.Notes is null);
        Assert.Contains(all, p => p.Id == second.Id);
    }

    [Fact]
    public async Task Patient_actions_are_logged_without_medical_notes()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreateAsync(admin);
        await admin.PutAsJsonAsync($"{Url}/{patient.Id}", Update(Valid() with { Notes = "Diabetic" }, patient.Version), Ct);
        await admin.DeleteAsync($"{Url}/{patient.Id}", Ct);

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=patients.&pageSize=100");
        var actions = new List<string>();
        foreach (var item in page.Items)
        {
            var details = await super.GetJsonAsync<ActivityLogDto>($"api/activity-logs/{item.Id}");
            if (details.EntityId == patient.Id.ToString())
            {
                actions.Add(details.Action);
                Assert.DoesNotContain("Diabetic", details.Details ?? string.Empty);
            }
        }

        Assert.Equal(["patients.created", "patients.deleted", "patients.updated"], actions.Order().ToArray());
    }

    private static CreatePatientRequest Valid() => new()
    {
        FullName = $"Patient {Guid.NewGuid():N}", Phone = "01000000000", Gender = Genders.Male,
    };

    private static UpdatePatientRequest Update(CreatePatientRequest request, int version) => new()
    {
        FullName = request.FullName, Phone = request.Phone, Notes = request.Notes,
        Gender = request.Gender, DateOfBirth = request.DateOfBirth, Version = version,
    };

    private static async Task<PatientDto> CreateAsync(HttpClient admin, string? fullName = null, string? phone = null)
    {
        var response = await admin.PostAsJsonAsync(Url, Valid() with
        {
            FullName = fullName ?? $"Patient {Guid.NewGuid():N}", Phone = phone ?? "01000000000",
        }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientDto>(Ct))!;
    }
}

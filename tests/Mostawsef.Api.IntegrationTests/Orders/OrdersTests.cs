using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Contracts.Settings;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Orders;

[Collection(ApiCollection.Name)]
public class OrdersTests(ApiFactory factory)
{
    private const string Url = "api/orders";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_order_for_an_existing_patient_copies_the_patient_and_the_prices()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var cbc = await CreateTestAsync(admin, price: 15000, labCost: 9000);
        var sugar = await CreateTestAsync(admin, price: 5000, labCost: 2000);
        var currency = (await admin.GetJsonAsync<ClinicSettingsDto>("api/clinic-settings")).CurrencyCode;

        var response = await admin.PostAsJsonAsync(Url, new CreateOrderRequest
        {
            PatientId = patient.Id,
            Items = [new() { LabTestId = cbc.Id }, new() { LabTestId = sugar.Id }],
            Addon = 3000,
        }, Ct);
        var order = await response.Content.ReadFromJsonAsync<OrderDetailsDto>(Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"{Url}/{order!.Id}", response.Headers.Location!.ToString());
        Assert.Matches(@"^ORD-\d{6,}$", order.Number);
        Assert.Equal((patient.Id, patient.FullName, patient.Phone), (order.PatientId, order.PatientName, order.PatientPhone));
        Assert.Equal((OrderStatuses.Pending, currency, 23000L, 0L, 23000L, 3000L),
            (order.Status, order.Currency, order.Total, order.Paid, order.Balance, order.Addon));
        Assert.Equal(TestUsers.Admin.Username, order.CreatedBy);
        Assert.Equal(
            [new OrderItemDto(cbc.Id, cbc.Code, cbc.Name, 15000), new OrderItemDto(sugar.Id, sugar.Code, sugar.Name, 5000)],
            order.Items);
        Assert.Equal(order, await admin.GetJsonAsync<OrderDetailsDto>($"{Url}/{order.Id}") with { CreatedAt = order.CreatedAt, Items = order.Items });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var labPrices = await db.OrderItems
            .Where(i => i.OrderId == order.Id).OrderByDescending(i => i.Price).Select(i => i.LabPrice).ToListAsync(Ct);
        Assert.Equal([9000L, 2000L], labPrices);
        Assert.Equal(23000L, await db.Orders.Where(o => o.Id == order.Id).Select(o => o.Total).SingleAsync(Ct));
    }

    [Theory]
    [InlineData(2500, "PartiallyPaid", 7500)]
    [InlineData(10000, "Paid", 0)]
    public async Task The_first_payment_sets_the_status_and_the_balance(long amount, string status, long balance)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var test = await CreateTestAsync(admin, price: 10000);

        var order = await CreateOrderAsync(admin, new CreateOrderRequest
        {
            PatientId = patient.Id,
            Items = [new() { LabTestId = test.Id }],
            FirstPayment = new() { Amount = amount, Method = PaymentMethods.Card, Notes = " Visa " },
        });

        Assert.Equal((status, amount, balance, 0L), (order.Status, order.Paid, order.Balance, order.Addon));
        var listed = (await admin.GetJsonAsync<PagedResult<OrderListItemDto>>($"{Url}?search={order.Number}")).Items.Single();
        Assert.Equal((1, 10000L, amount, status), (listed.TestsCount, listed.Total, listed.Paid, listed.Status));

        var reloaded = await admin.GetJsonAsync<OrderDetailsDto>($"{Url}/{order.Id}");
        Assert.Equal((status, amount, balance, 0L, 1), (reloaded.Status, reloaded.Paid, reloaded.Balance, reloaded.Addon, reloaded.Items.Count));
        Assert.Equal(test.Code, reloaded.Items[0].Code);
    }

    [Fact]
    public async Task Order_details_are_read_in_one_statement()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var test = await CreateTestAsync(admin);
        var order = await CreateOrderAsync(admin, new CreateOrderRequest
        {
            PatientId = patient.Id,
            Items = [new() { LabTestId = test.Id }],
        });

        factory.Sql.Clear();
        await admin.GetJsonAsync<OrderDetailsDto>($"{Url}/{order.Id}");

        Assert.Single(factory.Sql.Queries, q => q.Contains("FROM orders", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Order_item_labels_keep_the_name_from_order_time_after_a_rename()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var test = await CreateTestAsync(admin);
        var order = await CreateOrderAsync(admin, new CreateOrderRequest
        {
            PatientId = patient.Id,
            Items = [new() { LabTestId = test.Id }],
        });

        var renamed = await admin.PutAsJsonAsync($"api/lab-tests/{test.Id}", new UpdateLabTestRequest
        {
            Code = test.Code,
            Name = $"Renamed {test.Code}",
            Price = test.Price,
            LabCost = test.LabCost,
            IsActive = true,
            Version = test.Version,
        }, Ct);
        renamed.EnsureSuccessStatusCode();

        var details = await admin.GetJsonAsync<OrderDetailsDto>($"{Url}/{order.Id}");
        var item = Assert.Single(details.Items);
        Assert.Equal((test.Code, test.Name), (item.Code, item.Name));
    }

    [Fact]
    public async Task A_new_patient_is_created_with_the_order_and_both_actions_are_logged()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateTestAsync(admin);
        var name = $"New Patient {Guid.NewGuid():N}";

        var order = await CreateOrderAsync(admin, new CreateOrderRequest
        {
            NewPatient = new() { FullName = $"  {name} ", Phone = " ٠١٠ ١٢٣ ", Gender = Genders.Female, Notes = "Diabetic" },
            Items = [new() { LabTestId = test.Id }],
        });

        var patient = await admin.GetJsonAsync<PatientDto>($"api/patients/{order.PatientId}");
        Assert.Equal((name, "010 123"), (patient.FullName, patient.Phone));
        Assert.Equal((name, "010 123"), (order.PatientName, order.PatientPhone));

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var logged = new List<string>();
        var items = new List<ActivityLogListItemDto>();
        foreach (var action in new[] { "orders.created", "patients.created" })
        {
            items.AddRange((await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"api/activity-logs?search={action}&pageSize=20")).Items);
        }

        foreach (var item in items)
        {
            var details = await super.GetJsonAsync<ActivityLogDto>($"api/activity-logs/{item.Id}");
            if (details.EntityId == order.Id.ToString() || details.EntityId == order.PatientId.ToString())
            {
                logged.Add(details.Action);
                Assert.DoesNotContain("Diabetic", details.Details ?? string.Empty);
            }
        }

        Assert.Equal(["orders.created", "patients.created"], logged.Order().ToArray());
    }

    [Fact]
    public async Task Editing_the_patient_later_does_not_change_the_order()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var order = await CreateOrderAsync(admin, ForPatient(patient.Id, (await CreateTestAsync(admin)).Id));

        (await admin.PutAsJsonAsync($"api/patients/{patient.Id}", new UpdatePatientRequest
        {
            FullName = "Renamed", Phone = "0999", Gender = Genders.Male, Version = patient.Version,
        }, Ct)).EnsureSuccessStatusCode();

        var reread = await admin.GetJsonAsync<OrderDetailsDto>($"{Url}/{order.Id}");
        Assert.Equal((patient.FullName, patient.Phone), (reread.PatientName, reread.PatientPhone));
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_an_error_code_per_field()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, new CreateOrderRequest
        {
            NewPatient = new() { FullName = " ", Phone = "call me", Gender = "Other" },
            Items = [new() { LabTestId = null }],
            Addon = -1,
            FirstPayment = new() { Amount = 0, Method = "Cheque", Notes = new string('n', 501) },
        }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([ErrorCodes.Required], problem.Errors["NewPatient.FullName"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["NewPatient.Phone"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["NewPatient.Gender"]);
        Assert.Equal([ErrorCodes.Required], problem.Errors["Items[0].LabTestId"]);
        Assert.Equal([$"range|0|{OrderRules.MaxAddon}"], problem.Errors["Addon"]);
        Assert.Equal([$"range|1|{OrderRules.MaxPaymentAmount}"], problem.Errors["FirstPayment.Amount"]);
        Assert.Equal([ErrorCodes.Invalid], problem.Errors["FirstPayment.Method"]);
        Assert.Equal([$"max_length|{OrderRules.PaymentNotesMaxLength}"], problem.Errors["FirstPayment.Notes"]);
    }

    [Fact]
    public async Task An_order_without_tests_returns_400()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, new CreateOrderRequest { PatientId = patient.Id }, Ct));

        Assert.Equal([ErrorCodes.Required], problem.Errors["Items"]);
    }

    [Fact]
    public async Task An_order_with_too_many_tests_returns_400()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var items = Enumerable.Range(0, OrderRules.MaxItems + 1)
            .Select(_ => new CreateOrderItemRequest { LabTestId = Guid.NewGuid() })
            .ToList();

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(
            Url, new CreateOrderRequest { PatientId = patient.Id, Items = items }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([$"max_length|{OrderRules.MaxItems}"], problem.Errors["Items"]);
    }

    [Theory]
    [InlineData(false, ErrorCodes.Required)]
    [InlineData(true, ErrorCodes.Invalid)]
    public async Task The_patient_must_be_either_existing_or_new(bool both, string code)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateTestAsync(admin);
        var patient = await CreatePatientAsync(admin);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, new CreateOrderRequest
        {
            PatientId = both ? patient.Id : null,
            NewPatient = both ? NewPatient() : null,
            Items = [new() { LabTestId = test.Id }],
        }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([code], problem.Errors["PatientId"]);
    }

    [Fact]
    public async Task A_deleted_or_unknown_patient_returns_400_not_found()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateTestAsync(admin);
        var deleted = await CreatePatientAsync(admin);
        (await admin.DeleteAsync($"api/patients/{deleted.Id}", Ct)).EnsureSuccessStatusCode();

        foreach (var id in new[] { deleted.Id, Guid.NewGuid() })
        {
            var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, ForPatient(id, test.Id), Ct));

            Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
            Assert.Equal([ErrorCodes.NotFound], problem.Errors["PatientId"]);
        }
    }

    [Fact]
    public async Task Unknown_inactive_and_repeated_tests_are_refused_on_their_item()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var active = await CreateTestAsync(admin);
        var inactive = await CreateTestAsync(admin, isActive: false);

        async Task<string[]> SecondItemErrorAsync(Guid secondTestId) =>
            (await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, new CreateOrderRequest
            {
                PatientId = patient.Id,
                Items = [new() { LabTestId = active.Id }, new() { LabTestId = secondTestId }],
            }, Ct))).Errors["Items[1].LabTestId"];

        Assert.Equal([ErrorCodes.NotFound], await SecondItemErrorAsync(Guid.NewGuid()));
        Assert.Equal([ErrorCodes.Inactive], await SecondItemErrorAsync(inactive.Id));
        Assert.Equal([ErrorCodes.Duplicate], await SecondItemErrorAsync(active.Id));
    }

    [Fact]
    public async Task A_first_payment_over_the_total_returns_400()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var test = await CreateTestAsync(admin, price: 10000);

        var problem = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, ForPatient(patient.Id, test.Id) with
        {
            FirstPayment = new() { Amount = 10001, Method = PaymentMethods.Cash },
        }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal([ErrorCodes.ExceedsTotal], problem.Errors["FirstPayment.Amount"]);
    }

    [Fact]
    public async Task A_first_payment_may_cover_the_catalog_plus_addon()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var test = await CreateTestAsync(admin, price: 4000);

        var order = await CreateOrderAsync(admin, new CreateOrderRequest
        {
            PatientId = patient.Id,
            Items = [new() { LabTestId = test.Id }],
            Addon = 500,
            FirstPayment = new() { Amount = 4500, Method = PaymentMethods.Cash },
        });

        Assert.Equal((OrderStatuses.Paid, 4500L, 0L), (order.Status, order.Paid, order.Balance));

        var over = await ProblemResponse.ReadAsync(await admin.PostAsJsonAsync(Url, new CreateOrderRequest
        {
            PatientId = patient.Id,
            Items = [new() { LabTestId = test.Id }],
            Addon = 500,
            FirstPayment = new() { Amount = 4501, Method = PaymentMethods.Cash },
        }, Ct));

        Assert.Equal([ErrorCodes.ExceedsTotal], over.Errors["FirstPayment.Amount"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_refused_order_saves_nothing_not_even_the_new_patient(bool inactiveTest)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateTestAsync(admin, price: 1000, isActive: !inactiveTest);
        var request = new CreateOrderRequest
        {
            NewPatient = NewPatient(),
            Items = [new() { LabTestId = test.Id }],
            FirstPayment = inactiveTest ? null : new() { Amount = 5000, Method = PaymentMethods.Cash },
        };
        var ordersBefore = await CountOrdersAsync();

        var response = await admin.PostAsJsonAsync(Url, request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountPatientsNamedAsync(request.NewPatient!.FullName));
        Assert.Equal(ordersBefore, await CountOrdersAsync());
    }

    [Fact]
    public async Task A_failure_while_saving_rolls_back_the_new_patient_too()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var test = await CreateTestAsync(admin);
        var patient = await CreatePatientAsync(admin);
        var taken = await CreateOrderAsync(admin, ForPatient(patient.Id, test.Id));
        var takenNumber = long.Parse(taken.Number["ORD-".Length..], System.Globalization.CultureInfo.InvariantCulture);

        // Hands out a number that is already used, so the order insert fails on its unique index after the patient insert.
        using var broken = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<INumberSequence>(new FixedNumber(takenNumber))));
        var client = broken.CreateClient();
        client.WithBearer((await factory.CreateClient().LoginAsAsync(TestUsers.Admin)).AccessToken);
        var request = new CreateOrderRequest { NewPatient = NewPatient(), Items = [new() { LabTestId = test.Id }] };

        var response = await client.PostAsJsonAsync(Url, request, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await CountPatientsNamedAsync(request.NewPatient!.FullName));
    }

    [Fact]
    public async Task Without_a_token_returns_401()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Url}/{Guid.NewGuid()}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Url, new { }, Ct)).StatusCode);
    }

    [Theory]
    [InlineData("GET", Url)]
    [InlineData("GET", Url + "/{id}")]
    [InlineData("POST", Url)]
    public async Task Super_gets_403_on_every_order_endpoint(string method, string url)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var request = new HttpRequestMessage(new HttpMethod(method), url.Replace("{id}", Guid.NewGuid().ToString()))
        {
            Content = method == "POST" ? JsonContent.Create(new { }) : null,
        };

        Assert.Equal(HttpStatusCode.Forbidden, (await super.SendAsync(request, Ct)).StatusCode);
    }

    [Fact]
    public async Task Unknown_order_returns_404()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var problem = await ProblemResponse.ReadAsync(await admin.GetAsync($"{Url}/{Guid.NewGuid()}", Ct));

        Assert.Equal((HttpStatusCode.NotFound, ErrorCodes.NotFound), (problem.Status, problem.Code));
    }

    [Fact]
    public async Task The_list_is_newest_first_and_pages()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin);
        var test = await CreateTestAsync(admin);
        var older = await CreateOrderAsync(admin, ForPatient(patient.Id, test.Id));
        var newer = await CreateOrderAsync(admin, ForPatient(patient.Id, test.Id));

        var firstPage = await admin.GetJsonAsync<PagedResult<OrderListItemDto>>($"{Url}?page=1&pageSize=1&search={Uri.EscapeDataString(patient.FullName)}");
        var secondPage = await admin.GetJsonAsync<PagedResult<OrderListItemDto>>($"{Url}?page=2&pageSize=1&search={Uri.EscapeDataString(patient.FullName)}");

        Assert.Equal(2, firstPage.TotalCount);
        Assert.Equal(newer.Id, firstPage.Items.Single().Id);
        Assert.Equal(older.Id, secondPage.Items.Single().Id);
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

    [Theory]
    [InlineData("فاطمة")]
    [InlineData("فاطِمه")]
    public async Task Search_finds_the_patient_name_however_it_was_typed(string term)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var patient = await CreatePatientAsync(admin, $"فاطمة {Guid.NewGuid():N}");
        var order = await CreateOrderAsync(admin, ForPatient(patient.Id, (await CreateTestAsync(admin)).Id));

        var page = await admin.GetJsonAsync<PagedResult<OrderListItemDto>>($"{Url}?search={Uri.EscapeDataString(term)}&pageSize=100");

        Assert.Contains(page.Items, o => o.Id == order.Id);
    }

    [Fact]
    public async Task Search_matches_the_phone_in_any_digits_and_the_number()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);
        var digits = Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var patient = await CreatePatientAsync(admin, phone: "01" + digits);
        var order = await CreateOrderAsync(admin, ForPatient(patient.Id, (await CreateTestAsync(admin)).Id));
        var arabicDigits = string.Concat(digits.Select(d => (char)('\u0660' + (d - '0'))));

        var byPhone = await admin.GetJsonAsync<PagedResult<OrderListItemDto>>($"{Url}?search={Uri.EscapeDataString(arabicDigits)}");
        var byNumber = await admin.GetJsonAsync<PagedResult<OrderListItemDto>>($"{Url}?search={order.Number.ToLowerInvariant()}");

        Assert.Equal(order.Id, byPhone.Items.Single().Id);
        Assert.Equal(order.Id, byNumber.Items.Single().Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var searchText = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders
            .Where(o => o.Id == order.Id).Select(o => o.SearchText).SingleAsync(Ct);
        Assert.Contains(SearchText.Normalize(patient.Phone), searchText);
        Assert.Contains(SearchText.Normalize(order.Number), searchText);
    }

    private static CreatePatientRequest NewPatient() => new()
    {
        FullName = $"Patient {Guid.NewGuid():N}", Phone = "01000000000", Gender = Genders.Male,
    };

    private static CreateOrderRequest ForPatient(Guid patientId, Guid testId) =>
        new() { PatientId = patientId, Items = [new() { LabTestId = testId }] };

    private static async Task<OrderDetailsDto> CreateOrderAsync(HttpClient admin, CreateOrderRequest request)
    {
        var response = await admin.PostAsJsonAsync(Url, request, Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<OrderDetailsDto>(Ct))!;
    }

    private static async Task<PatientDto> CreatePatientAsync(HttpClient admin, string? fullName = null, string phone = "01000000000")
    {
        var response = await admin.PostAsJsonAsync("api/patients", NewPatient() with { FullName = fullName ?? $"Patient {Guid.NewGuid():N}", Phone = phone }, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PatientDto>(Ct))!;
    }

    private static async Task<LabTestDto> CreateTestAsync(HttpClient admin, long price = 1000, long labCost = 500, bool isActive = true)
    {
        var code = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var response = await admin.PostAsJsonAsync(
            "api/lab-tests", new CreateLabTestRequest { Code = code, Name = $"Test {code}", Price = price, LabCost = labCost }, Ct);
        response.EnsureSuccessStatusCode();
        var test = (await response.Content.ReadFromJsonAsync<LabTestDto>(Ct))!;
        if (isActive)
        {
            return test;
        }

        var update = await admin.PutAsJsonAsync($"api/lab-tests/{test.Id}", new UpdateLabTestRequest
        {
            Code = test.Code, Name = test.Name, Price = test.Price, LabCost = test.LabCost, IsActive = false, Version = test.Version,
        }, Ct);
        update.EnsureSuccessStatusCode();
        return (await update.Content.ReadFromJsonAsync<LabTestDto>(Ct))!;
    }

    private async Task<int> CountPatientsNamedAsync(string fullName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Patients
            .IgnoreQueryFilters().CountAsync(p => p.FullName == fullName, Ct);
    }

    private async Task<int> CountOrdersAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.CountAsync(Ct);
    }

    private sealed class FixedNumber(long value) : INumberSequence
    {
        public Task<long> NextAsync(string name, CancellationToken cancellationToken) => Task.FromResult(value);
    }
}

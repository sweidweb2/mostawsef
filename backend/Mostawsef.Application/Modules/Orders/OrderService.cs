using System.Globalization;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Patients;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Domain.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Modules.Orders;

/// <summary>Lab test orders (Admin portal). Failures throw <see cref="AppException"/>.</summary>
public interface IOrderService
{
    /// <summary>Newest first; <paramref name="filter"/> keeps only one status (one of <see cref="OrderStatuses"/>) and/or one patient's orders.</summary>
    Task<PagedResult<OrderListItemDto>> GetPageAsync(PageQuery query, OrderListFilterRequest filter, CancellationToken cancellationToken);

    Task<OrderDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<OrderStatsDto> GetStatsAsync(CancellationToken cancellationToken);

    /// <summary>Orders created per day for the last <paramref name="days"/> days (1 to <see cref="OrderService.MaxDailyDays"/>), today last; days without orders count 0.</summary>
    Task<IReadOnlyList<OrderDailyCountDto>> GetDailyCountsAsync(int days, CancellationToken cancellationToken);

    /// <summary>The <paramref name="take"/> (1 to <see cref="OrderService.MaxTopTests"/>) most ordered tests, most first.</summary>
    Task<IReadOnlyList<TopTestDto>> GetTopTestsAsync(int take, CancellationToken cancellationToken);

    /// <summary>Adds a payment of at most what the order still owes; returns the updated order.</summary>
    Task<OrderDetailsDto> AddPaymentAsync(Guid id, AddPaymentRequest request, CancellationToken cancellationToken);

    /// <summary>Creates the patient (if new), the order, its items and the first payment in one transaction.</summary>
    Task<OrderDetailsDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}

public class OrderService(
    IOrderRepository orders,
    IPatientRepository patients,
    ILabTestRepository tests,
    IClinicSettingsCache clinic,
    INumberSequence numbers,
    IActivityLogger activity,
    TimeProvider time) : IOrderService
{
    public const string NumberSequence = "orders";
    public const string NumberPrefix = "ORD";

    public async Task<PagedResult<OrderListItemDto>> GetPageAsync(PageQuery query, OrderListFilterRequest filter, CancellationToken cancellationToken)
    {
        query.EnsureSearchable();
        OrderStatus? wanted = null;
        if (!string.IsNullOrEmpty(filter.Status))
        {
            wanted = Enum.TryParse<OrderStatus>(filter.Status, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw AppException.Validation("status", ErrorCodes.Invalid);
        }

        var (items, total) = await orders.GetPageAsync(query, new OrderListFilter(wanted, filter.PatientId), cancellationToken);
        return new PagedResult<OrderListItemDto>(
            items.Select(o => new OrderListItemDto(
                o.Id, o.Number, o.CreatedAt, o.PatientName, o.PatientPhone, o.TestsCount, o.Total, o.Paid, o.Status.ToString())).ToList(),
            query.SafePage,
            query.SafePageSize,
            total);
    }

    public const int MaxDailyDays = 90;

    public const int MaxTopTests = 50;

    public async Task<IReadOnlyList<TopTestDto>> GetTopTestsAsync(int take, CancellationToken cancellationToken) =>
        (await orders.GetTopTestsAsync(Math.Clamp(take, 1, MaxTopTests), cancellationToken))
        .Select(t => new TopTestDto(t.LabTestId, t.Code, t.Name, t.Count))
        .ToList();

    public async Task<IReadOnlyList<OrderDailyCountDto>> GetDailyCountsAsync(int days, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, 1, MaxDailyDays);
        var zone = time.LocalTimeZone;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
        var first = today.AddDays(1 - days);

        // Midnight of the first day in the server's time zone, as UTC.
        var sinceUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(first.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
        var created = await orders.GetCreatedSinceAsync(sinceUtc, cancellationToken);

        var perDay = created
            .GroupBy(utc => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone)))
            .ToDictionary(g => g.Key, g => g.Count());

        return Enumerable.Range(0, days)
            .Select(offset => first.AddDays(offset))
            .Select(date => new OrderDailyCountDto(date, perDay.GetValueOrDefault(date)))
            .ToList();
    }

    public async Task<OrderDetailsDto> AddPaymentAsync(Guid id, AddPaymentRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.GetForPaymentAsync(id, cancellationToken) ?? throw AppException.NotFound();
        var amount = request.Amount!.Value;
        if (amount > order.Balance)
        {
            throw AppException.Validation(nameof(AddPaymentRequest.Amount), ErrorCodes.ExceedsBalance);
        }

        var payment = order.AddPayment(Enum.Parse<PaymentMethod>(request.Method), amount, time.GetUtcNow().UtcDateTime, request.Notes);
        orders.AddPayment(payment);
        order.UpdateStatus();
        await orders.SaveChangesAsync(cancellationToken);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.OrderPaymentAdded, ActivityModules.Orders, "Added a payment to an order")
            {
                EntityType = nameof(Order),
                EntityId = order.Id.ToString(),
                Details = new { order.Number, Amount = amount, request.Method, order.Total, order.Paid },
            },
            cancellationToken);

        return ToDetails(order); // the order is already loaded with its items and payments: no second read
    }

    public async Task<OrderStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var counts = await orders.GetStatusCountsAsync(cancellationToken);
        return new OrderStatsDto(
            counts.GetValueOrDefault(OrderStatus.Paid),
            counts.GetValueOrDefault(OrderStatus.PartiallyPaid),
            counts.GetValueOrDefault(OrderStatus.Pending));
    }

    public async Task<OrderDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(id, cancellationToken) ?? throw AppException.NotFound();
        return ToDetails(order);
    }

    public async Task<OrderDetailsDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // Clinic currency is cached (own context); patient and tests share the request DbContext so they stay sequential.
        var currencyTask = clinic.GetCurrencyCodeAsync(cancellationToken);
        OrderPatientRow patient;
        Patient? newPatient;
        IReadOnlyList<LabTest> lines;
        try
        {
            (patient, newPatient) = await ResolvePatientAsync(request, cancellationToken);
            lines = await LoadTestsAsync(request.Items, cancellationToken);
        }
        catch
        {
            await currencyTask.ObserveAsync();
            throw;
        }

        var currencyCode = await currencyTask;
        var total = TotalOf(request, lines); // validated before a number is taken, so a refused order never burns one
        var order = new Order(
            DocumentNumber.Format(NumberPrefix, await numbers.NextAsync(NumberSequence, cancellationToken)),
            patient.Id,
            patient.FullName,
            patient.SearchName,
            patient.Phone,
            currencyCode);
        Fill(order, request, lines, total);

        if (newPatient is not null)
        {
            patients.Add(newPatient);
        }

        orders.Add(order);
        await orders.SaveChangesAsync(cancellationToken);

        await LogCreatedAsync(order, newPatient, cancellationToken);
        return ToDetails(order);
    }

    /// <summary>The order total (tests plus add-on). A first payment over it is refused.</summary>
    private static long TotalOf(CreateOrderRequest request, IReadOnlyList<LabTest> lines)
    {
        var total = lines.Sum(t => t.Price) + (request.Addon ?? 0);
        if (request.FirstPayment?.Amount > total)
        {
            throw AppException.Validation(
                $"{nameof(CreateOrderRequest.FirstPayment)}.{nameof(CreateFirstPaymentRequest.Amount)}", ErrorCodes.ExceedsTotal);
        }

        return total;
    }

    /// <summary>Adds the items, total and first payment to a new order.</summary>
    private void Fill(Order order, CreateOrderRequest request, IReadOnlyList<LabTest> lines, long total)
    {
        var payment = request.FirstPayment;
        order.SetSearchText(SearchText.Key(order.PatientSearchName, order.PatientPhone, order.Number));
        foreach (var test in lines)
        {
            order.AddItem(test);
        }

        order.SetTotal(total);
        if (payment is not null)
        {
            order.AddPayment(Enum.Parse<PaymentMethod>(payment.Method), payment.Amount!.Value, time.GetUtcNow().UtcDateTime, payment.Notes);
        }

        order.UpdateStatus();
    }

    private async Task LogCreatedAsync(Order order, Patient? newPatient, CancellationToken cancellationToken)
    {
        if (newPatient is not null)
        {
            await activity.LogAsync(
                new ActivityEntry(ActivityActions.PatientCreated, ActivityModules.Patients, "Created a patient with an order")
                {
                    EntityType = nameof(Patient),
                    EntityId = newPatient.Id.ToString(),
                    Details = new { newPatient.FullName },
                },
                cancellationToken);
        }

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.OrderCreated, ActivityModules.Orders, "Created an order")
            {
                EntityType = nameof(Order),
                EntityId = order.Id.ToString(),
                Details = new { order.Number, order.PatientName, Tests = order.Items.Count, order.Total, order.Paid },
            },
            cancellationToken);
    }

    /// <summary>Existing patient snapshot, or a new patient the caller adds to the unit of work.</summary>
    private async Task<(OrderPatientRow Snapshot, Patient? NewPatient)> ResolvePatientAsync(
        CreateOrderRequest request, CancellationToken cancellationToken)
    {
        switch (request)
        {
            case { PatientId: null, NewPatient: null }:
                throw AppException.Validation(nameof(CreateOrderRequest.PatientId), ErrorCodes.Required);
            case { PatientId: not null, NewPatient: not null }:
                throw AppException.Validation(nameof(CreateOrderRequest.PatientId), ErrorCodes.Invalid);
            case { PatientId: { } id }:
                var existing = await patients.GetOrderPatientAsync(id, cancellationToken)
                    ?? throw AppException.Validation(nameof(CreateOrderRequest.PatientId), ErrorCodes.NotFound);
                return (existing, null);
            default:
                var newPatient = request.NewPatient!;
                PatientInput.EnsureValidDateOfBirth(
                    newPatient.DateOfBirth, time, $"{nameof(CreateOrderRequest.NewPatient)}.{nameof(newPatient.DateOfBirth)}");
                var created = PatientInput.Create(newPatient);
                return (new OrderPatientRow(created.Id, created.FullName, created.SearchName, created.Phone), created);
        }
    }

    /// <summary>The catalog test of each item, in the same order. Unknown, inactive or repeated tests fail on that item.</summary>
    private async Task<IReadOnlyList<LabTest>> LoadTestsAsync(IReadOnlyList<CreateOrderItemRequest> items, CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(items.Count);
        var unique = new List<Guid>(items.Count);
        var seen = new HashSet<Guid>();
        for (var i = 0; i < items.Count; i++)
        {
            var id = items[i].LabTestId!.Value;
            ids.Add(id);
            if (seen.Add(id))
            {
                unique.Add(id);
            }
        }

        var found = (await tests.GetByIdsAsync(unique, cancellationToken)).ToDictionary(t => t.Id);
        seen.Clear();
        var result = new List<LabTest>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            if (!seen.Add(ids[i]))
            {
                throw AppException.Validation(ItemField(i), ErrorCodes.Duplicate);
            }

            if (!found.TryGetValue(ids[i], out var test))
            {
                throw AppException.Validation(ItemField(i), ErrorCodes.NotFound);
            }

            if (!test.IsActive)
            {
                throw AppException.Validation(ItemField(i), ErrorCodes.Inactive);
            }

            result.Add(test);
        }

        return result;
    }

    private static string ItemField(int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{nameof(CreateOrderRequest.Items)}[{index}].{nameof(CreateOrderItemRequest.LabTestId)}");

    private static OrderDetailsDto ToDetails(OrderDetailsRow o) => new(
        o.Id, o.Number, o.CreatedAt, o.CreatedBy, o.PatientId, o.PatientName, o.PatientPhone, o.Status.ToString(), o.Currency,
        o.Total, o.Paid, o.Balance, o.Addon,
        o.Items.Select(i => new OrderItemDto(i.LabTestId, i.Code, i.Name, i.Price)).ToList(),
        o.Payments.Select(p => new OrderPaymentDto(p.PaidAt, p.Type.ToString(), p.Method.ToString(), p.Amount, p.Notes)).ToList());

    private static OrderDetailsDto ToDetails(Order o) => new(
        o.Id, o.Number, o.CreatedAt, o.CreatedBy, o.PatientId, o.PatientName, o.PatientPhone, o.Status.ToString(), o.Currency,
        o.Total, o.Paid, o.Balance, o.Addon,
        o.Items.Select(i => new OrderItemDto(i.LabTestId, i.LabTestCode, i.LabTestName, i.Price)).ToList(),
        o.Payments.OrderBy(p => p.PaidAt).ThenBy(p => p.Id).Select(p => new OrderPaymentDto(p.PaidAt, p.Type.ToString(), p.Method.ToString(), p.Amount, p.Notes)).ToList());
}

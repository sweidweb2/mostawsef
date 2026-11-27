using System.Globalization;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Patients;
using Mostawsef.Application.Modules.Settings;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;
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

    /// <summary>
    /// The Admin dashboard for a period (see <see cref="DashboardRangeQuery"/>): revenue and outstanding balance,
    /// orders created in it by payment state, orders and revenue per hour/day/month, and the most ordered tests.
    /// The period is kept between the first order's day and today.
    /// </summary>
    Task<OrderDashboardDto> GetDashboardAsync(DashboardRangeQuery query, CancellationToken cancellationToken);

    /// <summary>Adds a payment of at most what the order still owes; returns the updated order.</summary>
    Task<OrderDetailsDto> AddPaymentAsync(Guid id, AddPaymentRequest request, CancellationToken cancellationToken);

    /// <summary>Edits a payment and recalculates Paid and status; returns the updated order.</summary>
    Task<OrderDetailsDto> UpdatePaymentAsync(Guid id, Guid paymentId, UpdatePaymentRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes a payment and recalculates Paid and status; returns the updated order.</summary>
    Task<OrderDetailsDto> DeletePaymentAsync(Guid id, Guid paymentId, int? version, CancellationToken cancellationToken);

    /// <summary>Creates the patient (if new), the order, its items and the first payment in one transaction.</summary>
    Task<OrderDetailsDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);
}

public class OrderService(
    IOrderRepository orders,
    IPatientRepository patients,
    ILabTestRepository tests,
    IClinicSettingsService clinic,
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
            items.Select(ToListItem).ToList(),
            query.SafePage,
            query.SafePageSize,
            total);
    }

    public async Task<OrderDashboardDto> GetDashboardAsync(DashboardRangeQuery query, CancellationToken cancellationToken)
    {
        // One DbContext per request: the queries run one after another (each is a single indexed statement).
        var range = DashboardRange.Resolve(query, await orders.GetFirstCreatedAtAsync(cancellationToken), time);
        var currency = await clinic.GetCurrencyCodeAsync(cancellationToken);
        var counts = await orders.GetStatusCountsAsync(range.SinceUtc, range.UntilUtc, cancellationToken);
        var outstanding = await orders.GetOutstandingAsync(range.SinceUtc, range.UntilUtc, cancellationToken);
        var perHour = await orders.GetCreatedPerHourAsync(range.SinceUtc, range.UntilUtc, cancellationToken);
        var revenuePerHour = await orders.GetRevenuePerHourAsync(range.SinceUtc, range.UntilUtc, cancellationToken);
        var top = await orders.GetTopTestsAsync(OrderDashboardRules.TopTests, range.SinceUtc, range.UntilUtc, cancellationToken);

        var orderBuckets = range.Sum(perHour);
        var revenueBuckets = range.Sum(revenuePerHour);
        var orderPoints = new List<DashboardCountDto>();
        var revenuePoints = new List<DashboardMoneyDto>();
        long revenueTotal = 0;
        foreach (var start in range.BucketStarts())
        {
            orderPoints.Add(new DashboardCountDto(start, (int)orderBuckets.GetValueOrDefault(start)));
            var amount = revenueBuckets.GetValueOrDefault(start);
            revenuePoints.Add(new DashboardMoneyDto(start, amount));
            revenueTotal += amount;
        }

        return new OrderDashboardDto(
            currency,
            range.ToDto(),
            new OrderStatsDto(
                counts.GetValueOrDefault(OrderStatus.Paid),
                counts.GetValueOrDefault(OrderStatus.PartiallyPaid),
                counts.GetValueOrDefault(OrderStatus.Pending)),
            revenueTotal,
            outstanding,
            orderPoints,
            revenuePoints,
            top.Select(t => new TopTestDto(t.LabTestId, t.Code, t.Name, t.Count)).ToList());
    }

    public Task<OrderDetailsDto> AddPaymentAsync(Guid id, AddPaymentRequest request, CancellationToken cancellationToken) =>
        SaveOnceAsync(
            request.ClientRequestId,
            async (key, ct) => await orders.FindPaymentByClientRequestIdAsync(key, ct) is { } saved
                ? await GetAsync(saved.OrderId, ct)
                : null,
            () => SavePaymentAsync(id, request, cancellationToken),
            cancellationToken);

    private async Task<OrderDetailsDto> SavePaymentAsync(Guid id, AddPaymentRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.GetForPaymentAsync(id, cancellationToken) ?? throw AppException.NotFound();
        EnsureCurrentVersion(order, request.Version, nameof(AddPaymentRequest.Version));
        var amount = request.Amount!.Value;
        if (amount > order.Balance)
        {
            throw AppException.Validation(nameof(AddPaymentRequest.Amount), ErrorCodes.ExceedsBalance);
        }

        var payment = order.AddPayment(
            ParseMethod(request.Method, nameof(AddPaymentRequest.Method)),
            amount,
            time.GetUtcNow().UtcDateTime,
            request.Notes,
            request.ClientRequestId);
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

    public async Task<OrderDetailsDto> UpdatePaymentAsync(
        Guid id, Guid paymentId, UpdatePaymentRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.GetForPaymentAsync(id, cancellationToken) ?? throw AppException.NotFound();
        EnsureCurrentVersion(order, request.Version, nameof(UpdatePaymentRequest.Version));
        var payment = order.Payments.FirstOrDefault(p => p.Id == paymentId) ?? throw AppException.NotFound();
        var amount = request.Amount!.Value;
        if (payment.Type == PaymentType.Payment && amount > order.Balance + payment.Amount)
        {
            throw AppException.Validation(nameof(UpdatePaymentRequest.Amount), ErrorCodes.ExceedsBalance);
        }

        order.UpdatePayment(
            payment, ParseMethod(request.Method, nameof(UpdatePaymentRequest.Method)), amount, request.Notes);
        order.UpdateStatus();
        await orders.SaveChangesAsync(cancellationToken);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.OrderPaymentUpdated, ActivityModules.Orders, "Updated a payment on an order")
            {
                EntityType = nameof(Order),
                EntityId = order.Id.ToString(),
                Details = new { order.Number, PaymentId = paymentId, Amount = amount, request.Method, order.Total, order.Paid },
            },
            cancellationToken);

        return ToDetails(order);
    }

    public async Task<OrderDetailsDto> DeletePaymentAsync(Guid id, Guid paymentId, int? version, CancellationToken cancellationToken)
    {
        var order = await orders.GetForPaymentAsync(id, cancellationToken) ?? throw AppException.NotFound();
        EnsureCurrentVersion(order, version, "Version");
        var payment = order.Payments.FirstOrDefault(p => p.Id == paymentId) ?? throw AppException.NotFound();
        var amount = payment.Amount;
        var method = payment.Method;

        order.RemovePayment(payment);
        orders.RemovePayment(payment);
        order.UpdateStatus();
        await orders.SaveChangesAsync(cancellationToken);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.OrderPaymentDeleted, ActivityModules.Orders, "Deleted a payment from an order")
            {
                EntityType = nameof(Order),
                EntityId = order.Id.ToString(),
                Details = new { order.Number, PaymentId = paymentId, Amount = amount, Method = method.ToString(), order.Total, order.Paid },
            },
            cancellationToken);

        return ToDetails(order);
    }

    public async Task<OrderDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(id, cancellationToken) ?? throw AppException.NotFound();
        return ToDetails(order);
    }

    public Task<OrderDetailsDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken) =>
        SaveOnceAsync(
            request.ClientRequestId,
            async (key, ct) => await orders.FindByClientRequestIdAsync(key, ct) is { } saved ? ToDetails(saved) : null,
            () => SaveNewOrderAsync(request, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Saves at most once per client save attempt: a timed-out retry with the same <paramref name="clientRequestId"/>,
    /// also one that races the first attempt into the unique index (<see cref="ErrorCodes.Taken"/>), gets what the
    /// first attempt saved instead of a second order or payment.
    /// </summary>
    private static async Task<OrderDetailsDto> SaveOnceAsync(
        Guid? clientRequestId,
        Func<Guid, CancellationToken, Task<OrderDetailsDto?>> findSaved,
        Func<Task<OrderDetailsDto>> save,
        CancellationToken cancellationToken)
    {
        if (clientRequestId is not { } key)
        {
            return await save();
        }

        if (await findSaved(key, cancellationToken) is { } saved)
        {
            return saved;
        }

        try
        {
            return await save();
        }
        catch (AppException ex) when (ex.Code == ErrorCodes.Taken)
        {
            var raced = await findSaved(key, cancellationToken);
            if (raced is null)
            {
                throw;
            }

            return raced;
        }
    }

    private async Task<OrderDetailsDto> SaveNewOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // Clinic currency is cached (own context), so reading it first costs nothing.
        var currencyCode = await clinic.GetCurrencyCodeAsync(cancellationToken);

        // Everything is read and built inside the serializable transaction: the patient and the tests are read once
        // and stay valid until the save (a delete or deactivation cannot sneak in), the number is taken last so a
        // refused or failed order rolls it back (no gap), and a serialization retry starts again from fresh reads.
        Order order = null!;
        Patient? newPatient = null;
        await orders.ExecuteInSerializableAsync(
            async ct =>
            {
                OrderPatientRow patient;
                (patient, newPatient) = await ResolvePatientAsync(request, ct);
                var lines = await LoadTestsAsync(request.Items, ct);
                var addon = AddonOf(request, lines);

                order = new Order(
                    DocumentNumber.Format(NumberPrefix, await numbers.NextAsync(NumberSequence, ct)),
                    patient.Id,
                    patient.FullName,
                    patient.Phone,
                    currencyCode);
                order.SetSearchText(SearchText.Key(patient.SearchName, order.PatientPhone, order.Number));
                order.SetClientRequestId(request.ClientRequestId);
                Fill(order, request, lines, addon);
                if (newPatient is not null)
                {
                    patients.Add(newPatient);
                }

                orders.Add(order);
                await orders.SaveChangesAsync(ct);
            },
            cancellationToken);

        await LogCreatedAsync(order, newPatient, cancellationToken);
        return ToDetails(order);
    }

    /// <summary>
    /// Our price, after refusing a first payment above the patient total (our price plus the tests' lab costs).
    /// </summary>
    private static long AddonOf(CreateOrderRequest request, IReadOnlyList<LabTest> lines)
    {
        var addon = request.Addon ?? 0;
        var total = addon + lines.Sum(t => t.LabCost);
        if (request.FirstPayment?.Amount > total)
        {
            throw AppException.Validation(
                $"{nameof(CreateOrderRequest.FirstPayment)}.{nameof(CreateFirstPaymentRequest.Amount)}", ErrorCodes.ExceedsTotal);
        }

        return addon;
    }

    /// <summary>Adds the items, total and first payment to a new order.</summary>
    private void Fill(Order order, CreateOrderRequest request, IReadOnlyList<LabTest> lines, long addon)
    {
        var payment = request.FirstPayment;
        foreach (var test in lines)
        {
            order.AddItem(test);
        }

        order.SetCharges(addon);
        if (payment is not null)
        {
            order.AddPayment(
                ParseMethod(payment.Method, $"{nameof(CreateOrderRequest.FirstPayment)}.{nameof(CreateFirstPaymentRequest.Method)}"),
                payment.Amount!.Value,
                time.GetUtcNow().UtcDateTime,
                payment.Notes);
        }

        order.UpdateStatus();
    }

    private static PaymentMethod ParseMethod(string method, string field) =>
        Enum.TryParse<PaymentMethod>(method, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw AppException.Validation(field, ErrorCodes.Invalid);

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

    private static void EnsureCurrentVersion(Order order, int? version, string field)
    {
        if (version is null)
        {
            throw AppException.Validation(field, ErrorCodes.Required);
        }

        if (order.Version != version.Value)
        {
            throw AppException.Conflict(ErrorCodes.ConcurrencyConflict);
        }
    }

    private static OrderListItemDto ToListItem(OrderListRow o) => new(
        o.Id, o.Number, o.CreatedAt, o.PatientName, o.PatientPhone, o.TestsCount, o.Total, o.Paid, o.Status.ToString(), o.Currency);

    private static OrderDetailsDto ToDetails(OrderDetailsRow o) => new(
        o.Id, o.Number, o.CreatedAt, o.CreatedBy, o.PatientId, o.PatientName, o.PatientPhone, o.Status.ToString(), o.Currency,
        o.Total, o.Paid, o.Balance, o.Addon,
        o.Items.Select(i => new OrderItemDto(i.LabTestId, i.Code, i.Name, i.LabCost)).ToList(),
        o.Payments.Select(p => new OrderPaymentDto(p.Id, p.PaidAt, p.Type.ToString(), p.Method.ToString(), p.Amount, p.Notes)).ToList(),
        o.Version);

    private static OrderDetailsDto ToDetails(Order o) => new(
        o.Id, o.Number, o.CreatedAt, o.CreatedBy, o.PatientId, o.PatientName, o.PatientPhone, o.Status.ToString(), o.Currency,
        o.Total, o.Paid, o.Balance, o.Addon,
        o.Items.Select(i => new OrderItemDto(i.LabTestId, i.LabTestCode, i.LabTestName, i.LabPrice)).ToList(),
        o.Payments.OrderBy(p => p.PaidAt).ThenBy(p => p.Id)
            .Select(p => new OrderPaymentDto(p.Id, p.PaidAt, p.Type.ToString(), p.Method.ToString(), p.Amount, p.Notes)).ToList(),
        o.Version);
}

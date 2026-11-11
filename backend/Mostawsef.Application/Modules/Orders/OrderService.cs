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
    Task<PagedResult<OrderListItemDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    Task<OrderDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken);

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

    public async Task<PagedResult<OrderListItemDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        query.EnsureSearchable();
        var (items, total) = await orders.GetPageAsync(query, cancellationToken);
        return new PagedResult<OrderListItemDto>(
            items.Select(o => new OrderListItemDto(
                o.Id, o.Number, o.CreatedAt, o.PatientName, o.PatientPhone, o.TestsCount, o.Total, o.Paid, o.Status.ToString())).ToList(),
            query.SafePage,
            query.SafePageSize,
            total);
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
            try
            {
                await currencyTask;
            }
            catch
            {
                // Observe so a racing currency fault is not abandoned when validation fails first.
            }

            throw;
        }

        var currencyCode = await currencyTask;
        var total = lines.Sum(t => t.Price) + (request.Addon ?? 0);

        var payment = request.FirstPayment;
        if (payment?.Amount > total)
        {
            throw AppException.Validation(
                $"{nameof(CreateOrderRequest.FirstPayment)}.{nameof(CreateFirstPaymentRequest.Amount)}", ErrorCodes.ExceedsTotal);
        }

        var order = new Order(
            DocumentNumber.Format(NumberPrefix, await numbers.NextAsync(NumberSequence, cancellationToken)),
            patient.Id,
            patient.FullName,
            patient.SearchName,
            patient.Phone,
            currencyCode);
        order.SetSearchText(SearchText.Key(patient.SearchName, patient.Phone, order.Number));
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

        if (newPatient is not null)
        {
            patients.Add(newPatient);
        }

        orders.Add(order);
        await orders.SaveChangesAsync(cancellationToken);

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

        return ToDetails(order);
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
        o.Items.Select(i => new OrderItemDto(i.LabTestId, i.Code, i.Name, i.Price)).ToList());

    private static OrderDetailsDto ToDetails(Order o) => new(
        o.Id, o.Number, o.CreatedAt, o.CreatedBy, o.PatientId, o.PatientName, o.PatientPhone, o.Status.ToString(), o.Currency,
        o.Total, o.Paid, o.Balance, o.Addon,
        o.Items.Select(i => new OrderItemDto(i.LabTestId, i.LabTest?.Code ?? string.Empty, i.LabTest?.Name ?? string.Empty, i.Price)).ToList());
}

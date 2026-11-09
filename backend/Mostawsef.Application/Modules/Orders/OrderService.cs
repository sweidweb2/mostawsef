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
    IClinicCurrency currency,
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
        return new OrderDetailsDto(
            order.Id, order.Number, order.CreatedAt, order.CreatedBy, order.PatientId, order.PatientName, order.PatientPhone,
            order.Status.ToString(), order.Currency, order.Total, order.Paid, order.Total - order.Paid,
            order.Items.Select(i => new OrderItemDto(i.LabTestId, i.Code, i.Name, i.OurPrice)).ToList());
    }

    public async Task<OrderDetailsDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        // Currency uses its own context; patient and tests share the request DbContext so they stay sequential.
        var currencyTask = currency.GetCodeAsync(cancellationToken);
        var (patient, isNewPatient) = await ResolvePatientAsync(request, cancellationToken);
        var lines = await LoadTestsAsync(request.Items, cancellationToken);
        var currencyCode = await currencyTask;
        var total = lines.Sum(t => t.Price) + (request.OurPrice ?? 0);

        var payment = request.FirstPayment;
        if (payment?.Amount > total)
        {
            throw AppException.Validation(
                $"{nameof(CreateOrderRequest.FirstPayment)}.{nameof(CreateFirstPaymentRequest.Amount)}", ErrorCodes.ExceedsTotal);
        }

        var order = new Order(
            DocumentNumber.Format(NumberPrefix, await numbers.NextAsync(NumberSequence, cancellationToken)),
            patient,
            currencyCode);
        order.SetSearchText(SearchText.Key(patient.SearchName, patient.Phone, order.Number));
        foreach (var test in lines)
        {
            order.AddItem(test);
        }

        order.SetOurPrice(total);

        if (payment is not null)
        {
            order.AddPayment(Enum.Parse<PaymentMethod>(payment.Method), payment.Amount!.Value, time.GetUtcNow().UtcDateTime, payment.Notes);
        }

        order.UpdateStatus();

        if (isNewPatient)
        {
            patients.Add(patient);
        }

        orders.Add(order);
        await orders.SaveChangesAsync(cancellationToken);

        if (isNewPatient)
        {
            await activity.LogAsync(
                new ActivityEntry(ActivityActions.PatientCreated, ActivityModules.Patients, "Created a patient with an order")
                {
                    EntityType = nameof(Patient),
                    EntityId = patient.Id.ToString(),
                    Details = new { patient.FullName },
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

    /// <summary>The existing patient, or a new one that the caller adds to the unit of work.</summary>
    private async Task<(Patient Patient, bool IsNew)> ResolvePatientAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        switch (request)
        {
            case { PatientId: null, NewPatient: null }:
                throw AppException.Validation(nameof(CreateOrderRequest.PatientId), ErrorCodes.Required);
            case { PatientId: not null, NewPatient: not null }:
                throw AppException.Validation(nameof(CreateOrderRequest.PatientId), ErrorCodes.Invalid);
            case { PatientId: { } id }:
                return (await patients.GetByIdAsync(id, cancellationToken)
                    ?? throw AppException.Validation(nameof(CreateOrderRequest.PatientId), ErrorCodes.NotFound), false);
            default:
                var newPatient = request.NewPatient!;
                PatientInput.EnsureValidDateOfBirth(
                    newPatient.DateOfBirth, time, $"{nameof(CreateOrderRequest.NewPatient)}.{nameof(newPatient.DateOfBirth)}");
                return (PatientInput.Create(newPatient), true);
        }
    }

    /// <summary>The catalog test of each item, in the same order. Unknown, inactive or repeated tests fail on that item.</summary>
    private async Task<IReadOnlyList<LabTest>> LoadTestsAsync(IReadOnlyList<CreateOrderItemRequest> items, CancellationToken cancellationToken)
    {
        var ids = items.Select(i => i.LabTestId!.Value).ToList();
        var found = (await tests.GetByIdsAsync(ids.Distinct().ToList(), cancellationToken)).ToDictionary(t => t.Id);

        var seen = new HashSet<Guid>();
        var result = new List<LabTest>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            var field = $"{nameof(CreateOrderRequest.Items)}[{i}].{nameof(CreateOrderItemRequest.LabTestId)}";
            if (!seen.Add(ids[i]))
            {
                throw AppException.Validation(field, ErrorCodes.Duplicate);
            }

            if (!found.TryGetValue(ids[i], out var test))
            {
                throw AppException.Validation(field, ErrorCodes.NotFound);
            }

            if (!test.IsActive)
            {
                throw AppException.Validation(field, ErrorCodes.Inactive);
            }

            result.Add(test);
        }

        return result;
    }

    private static OrderDetailsDto ToDetails(Order o) => new(
        o.Id, o.Number, o.CreatedAt, o.CreatedBy, o.PatientId, o.PatientName, o.PatientPhone, o.Status.ToString(), o.Currency,
        o.Total, o.Paid, o.Balance,
        o.Items.Select(i => new OrderItemDto(i.LabTestId, i.LabTest?.Code ?? string.Empty, i.LabTest?.Name ?? string.Empty, i.OurPrice)).ToList());
}

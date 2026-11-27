using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

/// <summary>Fields copied onto a new order for an existing patient (no notes/tracking).</summary>
public record OrderPatientRow(Guid Id, string FullName, string SearchName, string Phone);

/// <summary>Deleted patients are invisible to every method.</summary>
public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Details projection including Notes (no tracking). Null when missing.</summary>
    Task<PatientDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Name/phone snapshot for creating an order (no tracking). Null when missing.</summary>
    Task<OrderPatientRow?> GetOrderPatientAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Ordered by SearchName. Search is LIKE on SearchText (trigram). Notes are not loaded.</summary>
    /// <param name="includeUnpaid">Also work out <see cref="PatientDto.HasUnpaidOrders"/> (an extra subquery); other callers, e.g. the new-order picker, leave it off.</param>
    Task<(IReadOnlyList<PatientDto> Items, int Total)> GetPageAsync(PageQuery query, bool includeUnpaid, CancellationToken cancellationToken);

    /// <summary>Up to <see cref="PageQuery.MaxExportRows"/> patients for print/export (no notes/audit).</summary>
    Task<IReadOnlyList<PatientDto>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>True if any order belongs to the patient (whatever its status).</summary>
    Task<bool> HasOrdersAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest orders for the patient details page (no second module dependency in the service).</summary>
    Task<IReadOnlyList<OrderListRow>> ListRecentOrdersAsync(Guid patientId, int take, CancellationToken cancellationToken);

    void Add(Patient patient);

    /// <summary>Soft delete, applied on save.</summary>
    void Remove(Patient patient);

    /// <summary>
    /// Soft-deletes <paramref name="patient"/> only when they still have no orders, inside a serializable
    /// transaction so a concurrent new order cannot sneak in between the check and the delete.
    /// Throws 404 when missing, 409 <c>patient_has_orders</c> when an order exists.
    /// </summary>
    Task SoftDeleteIfNoOrdersAsync(Patient patient, CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the patient changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

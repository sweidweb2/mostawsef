using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

/// <summary>Deleted patients are invisible to every method.</summary>
public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Ordered by full name. Search matches the normalized name, the phone or the guardian phone.</summary>
    Task<(IReadOnlyList<Patient> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    void Add(Patient patient);

    /// <summary>Soft delete, applied on save.</summary>
    void Remove(Patient patient);

    /// <summary>Throws AppException (409 concurrency_conflict) if the patient changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

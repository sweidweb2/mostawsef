using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface ILabTestRepository
{
    Task<LabTest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True if another test (not <paramref name="exceptId"/>) has this normalized code.</summary>
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>True if another test (not <paramref name="exceptId"/>) has this normalized name (SearchName).</summary>
    Task<bool> NameExistsAsync(string searchName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Ordered by name. Search matches the normalized name or the code.</summary>
    Task<(IReadOnlyList<LabTest> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    void Add(LabTest test);

    void Remove(LabTest test);

    /// <summary>Throws AppException (409 concurrency_conflict) if the test changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

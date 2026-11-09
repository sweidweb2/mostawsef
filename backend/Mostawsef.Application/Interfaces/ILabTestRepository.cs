using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface ILabTestRepository
{
    Task<LabTest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The tests with these ids that exist (tracked, so order items can reference them).</summary>
    Task<IReadOnlyList<LabTest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>True if an order item references the test.</summary>
    Task<bool> IsUsedAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True if another test (not <paramref name="exceptId"/>) has this normalized code.</summary>
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>True if another test (not <paramref name="exceptId"/>) has this normalized name (SearchName).</summary>
    Task<bool> NameExistsAsync(string searchName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Ordered by name. Search matches the normalized name or the code. <paramref name="active"/> filters IsActive when set.</summary>
    Task<(IReadOnlyList<LabTest> Items, int Total)> GetPageAsync(PageQuery query, bool? active, CancellationToken cancellationToken);

    void Add(LabTest test);

    void Remove(LabTest test);

    /// <summary>Throws AppException (409 concurrency_conflict) if the test changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

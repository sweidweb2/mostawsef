using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

/// <summary>One row of the lab-tests list.</summary>
public record LabTestListRow(
    Guid Id,
    string Code,
    string Name,
    long LabCost,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    int Version);

/// <summary>Columns the lab-tests PDF needs (no audit/version).</summary>
public record LabTestExportRow(
    Guid Id,
    string Code,
    string Name,
    long LabCost,
    bool IsActive);

public interface ILabTestRepository
{
    Task<LabTest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>List-row projection (no tracking). Null when missing.</summary>
    Task<LabTestListRow?> GetRowByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked test plus whether any order item references it. Null test when missing.</summary>
    Task<(LabTest? Test, bool IsUsed)> GetWithUsageAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The tests with these ids that exist (no tracking; order items copy snapshots only).</summary>
    Task<IReadOnlyList<LabTest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>How many of <paramref name="ids"/> are still active (for a last-moment create check).</summary>
    Task<int> CountActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Whether another test (not <paramref name="exceptId"/>) already has this code and/or SearchName.
    /// One statement; both flags can be true.
    /// </summary>
    Task<(bool CodeTaken, bool NameTaken)> FindDuplicatesAsync(
        string code, string searchName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>SearchNames that already exist (for import code races). Empty input returns an empty set.</summary>
    Task<HashSet<string>> FindExistingSearchNamesAsync(
        IReadOnlyCollection<string> searchNames, CancellationToken cancellationToken);

    /// <summary>Tracked tests with these SearchNames (for import updates). Empty input returns an empty list.</summary>
    Task<IReadOnlyList<LabTest>> GetTrackedBySearchNamesAsync(
        IReadOnlyCollection<string> searchNames, CancellationToken cancellationToken);

    /// <summary>Codes that already exist (for import code generation). Empty input returns an empty set.</summary>
    Task<HashSet<string>> FindExistingCodesAsync(
        IReadOnlyCollection<string> codes, CancellationToken cancellationToken);

    /// <summary>Ordered by SearchName then Code (unique indexes). Search is a prefix on SearchName or Code. <paramref name="active"/> filters IsActive when set.</summary>
    Task<(IReadOnlyList<LabTestListRow> Items, int Total)> GetPageAsync(PageQuery query, bool? active, CancellationToken cancellationToken);

    /// <summary>
    /// Up to <see cref="PageQuery.MaxExportRows"/> tests for print/export (no audit).
    /// When <paramref name="active"/> is set, only that IsActive value is returned (New Order catalog uses true).
    /// </summary>
    Task<IReadOnlyList<LabTestExportRow>> ListAllAsync(bool? active, CancellationToken cancellationToken);

    void Add(LabTest test);

    void AddRange(IEnumerable<LabTest> tests);

    void Remove(LabTest test);

    /// <summary>Drops tracked entities so an import can retry after a unique-index race.</summary>
    void DiscardChanges();

    /// <summary>Throws AppException (409 concurrency_conflict) if the test changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

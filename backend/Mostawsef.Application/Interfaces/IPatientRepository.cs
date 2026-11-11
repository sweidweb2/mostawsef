using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

/// <summary>One row of the patients list: Notes are omitted (medical data, loaded on GetById).</summary>
public record PatientListRow(
    Guid Id,
    string FullName,
    string Phone,
    string? GuardianPhone,
    bool IsInsured,
    Gender Gender,
    DateOnly? DateOfBirth,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    int Version);

/// <summary>Columns the patient PDF needs (no notes, no audit/version).</summary>
public record PatientExportRow(
    Guid Id,
    string FullName,
    string Phone,
    string? GuardianPhone,
    bool IsInsured,
    Gender Gender,
    DateOnly? DateOfBirth);

/// <summary>Fields copied onto a new order for an existing patient (no notes/tracking).</summary>
public record OrderPatientRow(Guid Id, string FullName, string SearchName, string Phone);

/// <summary>Patient details for a read-only GET (includes Notes, no tracking).</summary>
public record PatientDetailsRow(
    Guid Id,
    string FullName,
    string Phone,
    string? GuardianPhone,
    string? Notes,
    bool IsInsured,
    Gender Gender,
    DateOnly? DateOfBirth,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    int Version);

/// <summary>Deleted patients are invisible to every method.</summary>
public interface IPatientRepository
{
    Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Details projection including Notes (no tracking). Null when missing.</summary>
    Task<PatientDetailsRow?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Name/phone snapshot for creating an order (no tracking). Null when missing.</summary>
    Task<OrderPatientRow?> GetOrderPatientAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Ordered by SearchName. Search is LIKE on SearchText (trigram). Notes are not loaded.</summary>
    Task<(IReadOnlyList<PatientListRow> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    /// <summary>Up to <see cref="PageQuery.MaxExportRows"/> patients for print/export (no notes/audit).</summary>
    Task<IReadOnlyList<PatientExportRow>> ListAllAsync(CancellationToken cancellationToken);

    void Add(Patient patient);

    /// <summary>Soft delete, applied on save.</summary>
    void Remove(Patient patient);

    /// <summary>Throws AppException (409 concurrency_conflict) if the patient changed since it was loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

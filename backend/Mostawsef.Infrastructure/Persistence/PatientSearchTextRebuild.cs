using Microsoft.EntityFrameworkCore;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Rebuilds patients.SearchText after GuardianPhone was dropped so old guardian digits are no longer searchable.
/// One set-based <c>ExecuteUpdate</c> (no Version / UpdatedAt bump); a crash before the marker re-runs safely
/// because already-correct rows are skipped. Matches <c>Patient.ComposeSearchText</c>.
/// </summary>
public static class PatientSearchTextRebuild
{
    public const string Migration = "20261121092036_DropPatientGuardianPhoneAndIsInsured";

    /// <summary>Completion marker in <c>number_sequences</c> (not a real document counter).</summary>
    public const string CompletionMarker = "backfill.patient-search-no-guardian";

    /// <summary>True when the drop migration is applied but this rebuild has not finished.</summary>
    public static async Task<bool> ShouldRunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        if (!applied.Contains(Migration))
        {
            return false;
        }

        return !await DataBackfillMarker.ExistsAsync(db, CompletionMarker, cancellationToken);
    }

    /// <summary>Returns how many patients were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        // Same join as Patient.ComposeSearchText: name, phone, or "name\nphone" when both are present.
        var updated = await db.Patients.IgnoreQueryFilters()
            .Where(p => p.SearchText != (
                p.SearchName == string.Empty
                    ? p.Phone
                    : p.Phone == string.Empty
                        ? p.SearchName
                        : p.SearchName + "\n" + p.Phone))
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    p => p.SearchText,
                    p => p.SearchName == string.Empty
                        ? p.Phone
                        : p.Phone == string.Empty
                            ? p.SearchName
                            : p.SearchName + "\n" + p.Phone),
                cancellationToken);

        await DataBackfillMarker.EnsureAsync(db, CompletionMarker, cancellationToken);
        return updated;
    }
}

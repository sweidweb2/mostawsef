using Microsoft.EntityFrameworkCore;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Fills patients.SearchText for rows created before AddPatientSearchText (the column starts empty).
/// Plain EF Core instead of SQL in the migration. DatabaseInitializer runs it once after that migration.
/// </summary>
public static class PatientSearchTextBackfill
{
    public const string Migration = "20261109202905_AddPatientSearchText";

    public const int BatchSize = 500;

    /// <summary>Returns how many patients were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var updated = 0;
        Guid? lastId = null;
        while (true)
        {
            var query = db.Patients.IgnoreQueryFilters().Where(p => p.SearchText == string.Empty);
            if (lastId is { } after)
            {
                query = query.Where(p => p.Id.CompareTo(after) > 0);
            }

            var batch = await query.OrderBy(p => p.Id).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var patient in batch)
            {
                // Update rebuilds SearchText from the normalized name and phone already on the row.
                patient.Update(
                    patient.FullName, patient.SearchName, patient.Phone,
                    patient.Notes, patient.Gender, patient.DateOfBirth);
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            updated += batch.Count;

            if (batch.Count < BatchSize)
            {
                return updated;
            }

            lastId = batch[^1].Id;
        }
    }
}

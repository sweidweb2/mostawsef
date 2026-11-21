using Microsoft.EntityFrameworkCore;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Rebuilds patients.SearchText after GuardianPhone was dropped so old guardian digits are no longer searchable.
/// DatabaseInitializer runs it once after DropPatientGuardianPhoneAndIsInsured.
/// </summary>
public static class PatientSearchTextRebuild
{
    public const string Migration = "20261121092036_DropPatientGuardianPhoneAndIsInsured";

    public const int BatchSize = 500;

    /// <summary>Returns how many patients were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var updated = 0;
        Guid? lastId = null;
        while (true)
        {
            var query = db.Patients.IgnoreQueryFilters().AsQueryable();
            if (lastId is { } after)
            {
                query = query.Where(p => p.Id.CompareTo(after) > 0);
            }

            var batch = await query.OrderBy(p => p.Id).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var patient in batch)
            {
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

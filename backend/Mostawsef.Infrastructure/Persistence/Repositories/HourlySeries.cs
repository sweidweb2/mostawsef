using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// Dashboard series grouped per UTC hour in the database (a few thousand rows a year at most), so the Application layer
/// can place every hour in the clinic's local hour, day or month without loading the individual rows.
/// </summary>
internal static class HourlySeries
{
    /// <summary>How many of <paramref name="moments"/> (UTC) fall in each hour; hours without any are absent.</summary>
    public static async Task<IReadOnlyList<HourlyValue>> CountPerHourAsync(this IQueryable<DateTime> moments, CancellationToken cancellationToken) =>
        ToHours(await moments
            .GroupBy(t => new { t.Year, t.Month, t.Day, t.Hour })
            .Select(g => new HourRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Count()))
            .ToListAsync(cancellationToken));

    public static List<HourlyValue> ToHours(IEnumerable<HourRow> rows) =>
        rows.Select(r => new HourlyValue(new DateTime(r.Year, r.Month, r.Day, r.Hour, 0, 0, DateTimeKind.Utc), r.Value)).ToList();

    internal sealed record HourRow(int Year, int Month, int Day, int Hour, long Value);
}

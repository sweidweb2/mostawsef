using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;

namespace Mostawsef.Application.Common;

/// <summary>
/// A dashboard period resolved on the server: calendar days in the server's time zone, kept between the first
/// recorded day and today, the UTC window to query (<see cref="SinceUtc"/> inclusive, <see cref="UntilUtc"/> exclusive)
/// and the graph bucket. Records are grouped per UTC hour in the database and placed into local buckets here, so
/// every bucket follows the clinic's calendar (including daylight saving changes).
/// </summary>
public sealed class DashboardRange
{
    private DashboardRange(DateOnly from, DateOnly to, DateOnly firstDate, DateOnly today, string bucket, TimeZoneInfo zone)
    {
        From = from;
        To = to;
        FirstDate = firstDate;
        Today = today;
        Bucket = bucket;
        Zone = zone;
        SinceUtc = StartOfDayUtc(from, zone);
        UntilUtc = StartOfDayUtc(to.AddDays(1), zone);
    }

    public DateOnly From { get; }

    public DateOnly To { get; }

    public DateOnly FirstDate { get; }

    public DateOnly Today { get; }

    public string Bucket { get; }

    public TimeZoneInfo Zone { get; }

    public DateTime SinceUtc { get; }

    public DateTime UntilUtc { get; }

    public DashboardRangeDto ToDto() => new(From, To, FirstDate, Today, Bucket);

    /// <summary>
    /// Validates the request (From after To is refused) and keeps it within [first recorded day, today].
    /// <paramref name="firstRecordUtc"/> is the oldest record the dashboard counts; null when there is none.
    /// </summary>
    public static DashboardRange Resolve(DashboardRangeQuery query, DateTime? firstRecordUtc, TimeProvider time)
    {
        if (query.From is { } requestedFrom && query.To is { } requestedTo && requestedFrom > requestedTo)
        {
            throw AppException.Validation(nameof(DashboardRangeQuery.From), ErrorCodes.Invalid);
        }

        var zone = time.LocalTimeZone;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
        var firstDate = firstRecordUtc is { } first ? LocalDate(first, zone) : today;
        if (firstDate > today)
        {
            firstDate = today; // a record stamped ahead of this clock
        }

        var to = Clamp(query.To ?? today, firstDate, today);
        var from = Clamp(query.From ?? today.AddDays(1 - DashboardRangeRules.DefaultDays), firstDate, to);
        var days = to.DayNumber - from.DayNumber + 1;
        var bucket = days == 1 ? DashboardBuckets.Hour
            : days <= DashboardRangeRules.MaxDailyDays ? DashboardBuckets.Day
            : DashboardBuckets.Month;
        return new DashboardRange(from, to, firstDate, today, bucket, zone);
    }

    /// <summary>Every bucket start of the period in order (local time, Unspecified kind).</summary>
    public IReadOnlyList<DateTime> BucketStarts()
    {
        var starts = new List<DateTime>();
        switch (Bucket)
        {
            case DashboardBuckets.Hour:
                var day = From.ToDateTime(TimeOnly.MinValue);
                for (var hour = 0; hour < 24; hour++)
                {
                    starts.Add(day.AddHours(hour));
                }

                break;
            case DashboardBuckets.Day:
                for (var date = From; date <= To; date = date.AddDays(1))
                {
                    starts.Add(date.ToDateTime(TimeOnly.MinValue));
                }

                break;
            default:
                for (var month = new DateTime(From.Year, From.Month, 1); month <= To.ToDateTime(TimeOnly.MinValue); month = month.AddMonths(1))
                {
                    starts.Add(month);
                }

                break;
        }

        return starts;
    }

    /// <summary>The local start of the bucket a UTC moment falls in.</summary>
    public DateTime BucketOf(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        return Bucket switch
        {
            DashboardBuckets.Hour => new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0),
            DashboardBuckets.Day => local.Date,
            _ => new DateTime(local.Year, local.Month, 1),
        };
    }

    /// <summary>Sums per-UTC-hour values into this period's buckets, keyed by bucket start.</summary>
    public Dictionary<DateTime, long> Sum(IReadOnlyList<HourlyValue> hours)
    {
        var sums = new Dictionary<DateTime, long>();
        foreach (var hour in hours)
        {
            var bucket = BucketOf(hour.HourUtc);
            sums[bucket] = sums.GetValueOrDefault(bucket) + hour.Value;
        }

        return sums;
    }

    private static DateOnly Clamp(DateOnly value, DateOnly min, DateOnly max) =>
        value < min ? min : value > max ? max : value;

    private static DateOnly LocalDate(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));

    /// <summary>
    /// The UTC moment a local day starts. Where daylight saving skips midnight (clocks jump 00:00 to 01:00), the day
    /// starts at the first local time that exists.
    /// </summary>
    public static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        for (var i = 0; i < 4 && zone.IsInvalidTime(local); i++)
        {
            local = local.AddMinutes(30);
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
    }
}

using System.Globalization;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Dashboards;

namespace Mostawsef.Api.IntegrationTests.Orders;

/// <summary>How a dashboard period is kept in bounds and split into buckets (no database).</summary>
public class DashboardRangeTests
{
    private sealed class FixedTime(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => zone;
    }

    private static readonly TimeProvider Clock = new FixedTime(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);

    private static readonly DateTime FirstRecord = new(2025, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    private static DashboardRange Resolve(string? from, string? to, DateTime? first = null) =>
        DashboardRange.Resolve(
            new DashboardRangeQuery { From = from is null ? null : DateOnly.Parse(from, CultureInfo.InvariantCulture), To = to is null ? null : DateOnly.Parse(to, CultureInfo.InvariantCulture) },
            first ?? FirstRecord,
            Clock);

    [Theory]
    [InlineData("2026-11-15", "2026-11-15", DashboardBuckets.Hour, 24)]
    [InlineData("2026-11-01", "2026-11-15", DashboardBuckets.Day, 15)]
    [InlineData("2026-08-16", "2026-11-15", DashboardBuckets.Day, 92)]
    [InlineData("2026-08-15", "2026-11-15", DashboardBuckets.Month, 4)]
    [InlineData("2026-01-01", "2026-12-31", DashboardBuckets.Month, 11)]
    public void The_bucket_follows_the_period_length(string from, string to, string bucket, int points)
    {
        var range = Resolve(from, to);

        Assert.Equal(bucket, range.Bucket);
        Assert.Equal(points, range.BucketStarts().Count);
    }

    [Fact]
    public void The_period_never_starts_before_the_first_record_nor_ends_after_today()
    {
        var range = Resolve("2001-01-01", "2030-01-01");

        Assert.Equal((new DateOnly(2025, 3, 10), new DateOnly(2026, 11, 15)), (range.From, range.To));
        Assert.Equal((new DateOnly(2025, 3, 10), new DateOnly(2026, 11, 15)), (range.FirstDate, range.Today));
        Assert.Equal(new DateTime(2025, 3, 1), range.BucketStarts()[0]);
    }

    [Fact]
    public void Without_records_or_dates_the_period_is_today()
    {
        var range = DashboardRange.Resolve(new DashboardRangeQuery(), null, Clock);

        Assert.Equal((new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 15)), (range.From, range.To));
        Assert.Equal(DashboardBuckets.Hour, range.Bucket);
    }

    [Fact]
    public void From_after_to_is_refused()
    {
        var error = Assert.Throws<AppException>(() => Resolve("2026-11-10", "2026-11-01"));

        Assert.Equal(("From", Mostawsef.Contracts.Common.ErrorCodes.Invalid), (error.Field, error.Code));
    }

    [Fact]
    public void Hours_are_summed_into_local_buckets()
    {
        var cairo = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo");
        var time = new FixedTime(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero), cairo);
        var range = DashboardRange.Resolve(
            new DashboardRangeQuery { From = new DateOnly(2026, 11, 14), To = new DateOnly(2026, 11, 15) }, FirstRecord, time);

        // 23:00 UTC on the 14th is already the 15th in Cairo (UTC+2 in November).
        var sums = range.Sum([new HourlyValue(new DateTime(2026, 11, 14, 23, 0, 0, DateTimeKind.Utc), 3)]);

        Assert.Equal(3, sums[new DateTime(2026, 11, 15)]);
        Assert.Equal(new DateTime(2026, 11, 13, 22, 0, 0, DateTimeKind.Utc), range.SinceUtc);
    }

    [Fact]
    public void A_day_whose_midnight_is_skipped_by_daylight_saving_starts_at_its_first_real_hour()
    {
        var cairo = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Egypt Standard Time" : "Africa/Cairo");

        // Egypt moves clocks from 00:00 to 01:00 on the last Friday of April (2026-04-24).
        var start = DashboardRange.StartOfDayUtc(new DateOnly(2026, 4, 24), cairo);

        Assert.Equal(new DateTime(2026, 4, 23, 22, 0, 0, DateTimeKind.Utc), start);
    }
}

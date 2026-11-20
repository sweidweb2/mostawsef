namespace Mostawsef.Contracts.Dashboards;

/// <summary>How a dashboard period is split for its graphs (chosen by the API from the period length).</summary>
public static class DashboardBuckets
{
    /// <summary>A single day: one point per hour.</summary>
    public const string Hour = "Hour";

    /// <summary>Up to <see cref="DashboardRangeRules.MaxDailyDays"/> days: one point per day.</summary>
    public const string Day = "Day";

    /// <summary>Longer periods: one point per month.</summary>
    public const string Month = "Month";
}

public static class DashboardRangeRules
{
    /// <summary>Periods up to this many days are drawn per day (a quarter); longer ones per month.</summary>
    public const int MaxDailyDays = 92;

    /// <summary>The period when the request names none: the last 30 days.</summary>
    public const int DefaultDays = 30;
}

/// <summary>
/// A dashboard period: calendar days in the server's time zone, both included. Missing values default to the last
/// <see cref="DashboardRangeRules.DefaultDays"/> days; both are kept between the first recorded day and today.
/// <see cref="From"/> after <see cref="To"/> is refused (400 "invalid" on From).
/// </summary>
public record DashboardRangeQuery
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}

/// <summary>
/// The period the figures cover (after the API kept it within the bounds), the bounds a period may use, and how the
/// graphs are split (<see cref="DashboardBuckets"/>). <see cref="FirstDate"/> is the day of the first record
/// (today when there is none); <see cref="Today"/> is the server's current day.
/// </summary>
public record DashboardRangeDto(DateOnly From, DateOnly To, DateOnly FirstDate, DateOnly Today, string Bucket);

/// <summary>One graph point: the start of its hour, day or month (server local time) and the count in it.</summary>
public record DashboardCountDto(DateTime Start, int Count);

/// <summary>One money graph point: the start of its hour, day or month (server local time) and the amount in minor units.</summary>
public record DashboardMoneyDto(DateTime Start, long Amount);

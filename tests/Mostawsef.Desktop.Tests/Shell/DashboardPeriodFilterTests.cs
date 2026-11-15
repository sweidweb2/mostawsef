using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Tests.Shell;

/// <summary>The dashboards' period picker: each kind of period, the bounds (first record, today) and when it reloads.</summary>
public class DashboardPeriodFilterTests
{
    private static readonly DateOnly Today = new(2026, 11, 15); // a Sunday

    private static DashboardPeriodFilter Filter(DashboardPeriod period, out List<(DateOnly From, DateOnly To)> changes)
    {
        var filter = new DashboardPeriodFilter(Today);
        var seen = new List<(DateOnly, DateOnly)>();
        filter.Changed += (_, _) => seen.Add((filter.From, filter.To));
        filter.SelectedPeriod = DashboardPeriodFilter.PeriodOptions.Single(o => o.Period == period);
        changes = seen;
        return filter;
    }

    [Fact]
    public void Starts_on_this_month_up_to_today()
    {
        var filter = new DashboardPeriodFilter(Today);

        Assert.Equal(DashboardPeriod.Month, filter.Period);
        Assert.Equal((new DateOnly(2026, 11, 1), Today), (filter.From, filter.To));
        Assert.Equal([new DateOnly(2026, 11, 1)], filter.Months);
        Assert.Equal([2026], filter.Years);
    }

    [Fact]
    public void A_day_is_that_day_and_a_future_day_becomes_today()
    {
        var filter = Filter(DashboardPeriod.Day, out var changes);
        filter.SelectedDay = new DateOnly(2026, 11, 3);
        Assert.Equal((new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 3)), (filter.From, filter.To));

        filter.SelectedDay = new DateOnly(2027, 1, 1);
        Assert.Equal(Today, filter.SelectedDay);
        Assert.Equal((Today, Today), changes[^1]);
    }

    [Fact]
    public void A_week_runs_saturday_to_friday_and_stops_at_today()
    {
        var filter = Filter(DashboardPeriod.Week, out _);
        Assert.Equal((new DateOnly(2026, 11, 14), Today), (filter.From, filter.To));

        filter.SelectedWeekDay = new DateOnly(2026, 11, 11); // Wednesday
        Assert.Equal((new DateOnly(2026, 11, 7), new DateOnly(2026, 11, 13)), (filter.From, filter.To));
    }

    [Fact]
    public void A_year_runs_from_january_to_today_for_the_current_year()
    {
        var filter = Filter(DashboardPeriod.Year, out _);

        Assert.Equal((new DateOnly(2026, 1, 1), Today), (filter.From, filter.To));
    }

    [Fact]
    public void A_custom_range_keeps_from_before_to_and_to_not_after_today()
    {
        var filter = Filter(DashboardPeriod.Custom, out _);
        filter.CustomTo = new DateOnly(2026, 11, 10);
        filter.CustomFrom = new DateOnly(2026, 11, 12);
        Assert.Equal(new DateOnly(2026, 11, 10), filter.CustomFrom);

        filter.CustomTo = new DateOnly(2026, 12, 31);
        Assert.Equal(Today, filter.CustomTo);
        Assert.Equal((new DateOnly(2026, 11, 10), Today), (filter.From, filter.To));
    }

    [Fact]
    public void The_servers_bounds_fill_the_lists_and_keep_choices_after_the_first_record()
    {
        var filter = Filter(DashboardPeriod.Custom, out var changes);
        filter.CustomFrom = new DateOnly(2024, 1, 1);
        changes.Clear();

        filter.ApplyBounds(new DateOnly(2025, 10, 20), Today);

        Assert.Equal(14, filter.Months.Count);
        Assert.Equal((new DateOnly(2026, 11, 1), new DateOnly(2025, 10, 1)), (filter.Months[0], filter.Months[^1]));
        Assert.Equal([2026, 2025], filter.Years);
        Assert.Equal(new DateOnly(2025, 10, 20), filter.CustomFrom);
        Assert.Equal(new DateTime(2025, 10, 20), filter.MinDate);
        Assert.Equal([(new DateOnly(2025, 10, 20), Today)], changes);

        filter.SelectedDay = new DateOnly(2020, 5, 5);
        Assert.Equal(new DateOnly(2025, 10, 20), filter.SelectedDay);
    }

    [Fact]
    public void Bounds_that_leave_the_period_as_it_was_do_not_ask_for_a_reload()
    {
        var filter = new DashboardPeriodFilter(Today);
        var changed = 0;
        filter.Changed += (_, _) => changed++;
        var selected = filter.SelectedMonth;

        filter.ApplyBounds(new DateOnly(2024, 6, 1), Today);

        Assert.Equal(0, changed);
        Assert.Equal(selected, filter.SelectedMonth);
        Assert.Equal(new DateOnly(2024, 6, 1), filter.Months[^1]);
    }
}

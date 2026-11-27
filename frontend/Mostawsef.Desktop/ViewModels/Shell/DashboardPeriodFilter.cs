using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Desktop.Components;

namespace Mostawsef.Desktop.ViewModels.Shell;

public enum DashboardPeriod
{
    Day,
    Week,
    Month,
    Year,
    Custom,
}

/// <summary>A choice of the period dropdown (<c>Dashboard.Period.*</c> label).</summary>
public sealed record DashboardPeriodOption(DashboardPeriod Period, string LabelKey);

/// <summary>
/// The period picker shared by the Admin and Super dashboards: a day, a week (Saturday to Friday), a month, a year or
/// a custom From/To range. Each kind keeps its own choice, so switching back restores it. Dates stay between the first
/// recorded day and today: both come from the server's answer (<see cref="ApplyBounds"/>), and until the first answer
/// only "not after today" applies (the server keeps the period in bounds too). <see cref="Changed"/> fires only when
/// the resulting period (<see cref="From"/>..<see cref="To"/>) really changes, so the page loads once per real change.
/// </summary>
public sealed partial class DashboardPeriodFilter : ObservableObject
{
    /// <summary>The clinic's week: Saturday to Friday.</summary>
    public const DayOfWeek WeekStart = DayOfWeek.Saturday;

    /// <summary>How many days the custom range starts with.</summary>
    public const int DefaultCustomDays = DashboardRangeRules.DefaultDays;

    private DateOnly? _firstDate;
    private DateOnly _today;
    private (DateOnly From, DateOnly To) _range;
    private bool _batch;

    public DashboardPeriodFilter(DateOnly today)
    {
        _today = today;
        _batch = true;
        SelectedDay = today;
        SelectedWeekDay = today;
        SelectedMonth = FirstOfMonth(today);
        SelectedYear = today.Year;
        CustomFrom = today.AddDays(1 - DefaultCustomDays);
        CustomTo = today;
        SyncLists();
        _batch = false;
        _range = Compute();
    }

    public static IReadOnlyList<DashboardPeriodOption> PeriodOptions { get; } =
    [
        new(DashboardPeriod.Day, "Dashboard.Period.Day"),
        new(DashboardPeriod.Week, "Dashboard.Period.Week"),
        new(DashboardPeriod.Month, "Dashboard.Period.Month"),
        new(DashboardPeriod.Year, "Dashboard.Period.Year"),
        new(DashboardPeriod.Custom, "Dashboard.Period.Custom"),
    ];

    /// <summary>Raised after <see cref="From"/>/<see cref="To"/> changed because of a choice (not by the server's bounds alone when they leave the period as it was).</summary>
    public event EventHandler? Changed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Period), nameof(IsDay), nameof(IsWeek), nameof(IsMonth), nameof(IsYear), nameof(IsCustom))]
    public partial DashboardPeriodOption SelectedPeriod { get; set; } = PeriodOptions[2];

    public DashboardPeriod Period => SelectedPeriod?.Period ?? DashboardPeriod.Month;

    public bool IsDay => Period == DashboardPeriod.Day;

    public bool IsWeek => Period == DashboardPeriod.Week;

    public bool IsMonth => Period == DashboardPeriod.Month;

    public bool IsYear => Period == DashboardPeriod.Year;

    public bool IsCustom => Period == DashboardPeriod.Custom;

    [ObservableProperty]
    public partial DateOnly? SelectedDay { get; set; }

    /// <summary>Any day of the wanted week.</summary>
    [ObservableProperty]
    public partial DateOnly? SelectedWeekDay { get; set; }

    /// <summary>The first day of the wanted month (one of <see cref="Months"/>).</summary>
    [ObservableProperty]
    public partial DateOnly? SelectedMonth { get; set; }

    /// <summary>One of <see cref="Years"/>.</summary>
    [ObservableProperty]
    public partial int? SelectedYear { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomToMin))]
    public partial DateOnly? CustomFrom { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CustomFromMax))]
    public partial DateOnly? CustomTo { get; set; }

    /// <summary>Months from the current one back to the first recorded one (newest first).</summary>
    public ObservableCollection<DateOnly> Months { get; } = [];

    /// <summary>Years from the current one back to the first recorded one (newest first).</summary>
    public ObservableCollection<int> Years { get; } = [];

    /// <summary>Earliest day a picker offers: the first recorded day (none until the server answered).</summary>
    public DateTime? MinDate => _firstDate?.ToDateTime(TimeOnly.MinValue);

    /// <summary>Latest day a picker offers: today.</summary>
    public DateTime MaxDate => _today.ToDateTime(TimeOnly.MinValue);

    public DateTime CustomFromMax => (CustomTo ?? _today).ToDateTime(TimeOnly.MinValue);

    public DateTime? CustomToMin => (CustomFrom ?? _firstDate)?.ToDateTime(TimeOnly.MinValue);

    /// <summary>First day of the chosen period (kept within bounds).</summary>
    public DateOnly From => _range.From;

    /// <summary>Last day of the chosen period (never after today).</summary>
    public DateOnly To => _range.To;

    /// <summary>The graphs' point type for the server's bucket name (<see cref="DashboardBuckets"/>).</summary>
    public static ChartUnit UnitOf(string? bucket) => bucket switch
    {
        DashboardBuckets.Hour => ChartUnit.Hour,
        DashboardBuckets.Month => ChartUnit.Month,
        _ => ChartUnit.Day,
    };

    /// <summary>The week (Saturday to Friday) that contains <paramref name="day"/>.</summary>
    public static (DateOnly From, DateOnly To) WeekOf(DateOnly day)
    {
        var start = day.AddDays(-(((int)day.DayOfWeek - (int)WeekStart + 7) % 7));
        return (start, start.AddDays(6));
    }

    /// <summary>
    /// Takes the server's first recorded day and today. Lists and pickers then offer only those days, and choices
    /// outside them move to the nearest valid day. Fires <see cref="Changed"/> only if the period itself moved.
    /// </summary>
    public void ApplyBounds(DateOnly firstDate, DateOnly today)
    {
        if (firstDate > today)
        {
            firstDate = today;
        }

        if (_firstDate == firstDate && _today == today)
        {
            return;
        }

        _batch = true;
        try
        {
            _firstDate = firstDate;
            _today = today;
            SyncLists();
            SelectedDay = ClampDay(SelectedDay);
            SelectedWeekDay = ClampDay(SelectedWeekDay);
            SelectedMonth = SelectedMonth is { } month && Months.Contains(month) ? month : NearestMonth(SelectedMonth);
            SelectedYear = SelectedYear is { } year && Years.Contains(year) ? year : NearestYear(SelectedYear);
            CustomTo = ClampDay(CustomTo);
            CustomFrom = CustomFrom is { } from ? ClampCustomFrom(from) : null;
        }
        finally
        {
            _batch = false;
        }

        OnPropertyChanged(nameof(MinDate));
        OnPropertyChanged(nameof(MaxDate));
        OnPropertyChanged(nameof(CustomFromMax));
        OnPropertyChanged(nameof(CustomToMin));
        Update();
    }

    partial void OnSelectedPeriodChanged(DashboardPeriodOption value) => Update();

    partial void OnSelectedDayChanged(DateOnly? value) => Coerce(value, ClampDay(value), v => SelectedDay = v);

    partial void OnSelectedWeekDayChanged(DateOnly? value) => Coerce(value, ClampDay(value), v => SelectedWeekDay = v);

    partial void OnSelectedMonthChanged(DateOnly? value) => Update();

    partial void OnSelectedYearChanged(int? value) => Update();

    partial void OnCustomFromChanged(DateOnly? value) =>
        Coerce(value, value is { } v ? ClampCustomFrom(v) : null, v => CustomFrom = v);

    partial void OnCustomToChanged(DateOnly? value) =>
        Coerce(value, value is { } v ? Clamp(v, CustomFrom ?? _firstDate ?? DateOnly.MinValue, _today) : null, v => CustomTo = v);

    private DateOnly ClampCustomFrom(DateOnly value)
    {
        var to = CustomTo ?? _today;
        var min = _firstDate ?? DateOnly.MinValue;
        var earliest = to.AddDays(1 - DashboardRangeRules.MaxSpanDays);
        return Clamp(value, earliest > min ? earliest : min, to);
    }

    /// <summary>A typed or picked day outside the bounds is replaced by the nearest valid one (which runs this again).</summary>
    private void Coerce(DateOnly? value, DateOnly? valid, Action<DateOnly?> set)
    {
        if (!_batch && value != valid)
        {
            set(valid);
            return;
        }

        Update();
    }

    private void Update()
    {
        if (_batch)
        {
            return;
        }

        var range = Compute();
        if (range == _range)
        {
            return;
        }

        _range = range;
        OnPropertyChanged(nameof(From));
        OnPropertyChanged(nameof(To));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private (DateOnly From, DateOnly To) Compute()
    {
        var (from, to) = Period switch
        {
            DashboardPeriod.Day => (SelectedDay ?? _today, SelectedDay ?? _today),
            DashboardPeriod.Week => WeekOf(SelectedWeekDay ?? _today),
            DashboardPeriod.Year => (new DateOnly(SelectedYear ?? _today.Year, 1, 1), new DateOnly(SelectedYear ?? _today.Year, 12, 31)),
            DashboardPeriod.Custom => (CustomFrom ?? _today.AddDays(1 - DefaultCustomDays), CustomTo ?? _today),
            _ => (SelectedMonth ?? FirstOfMonth(_today), (SelectedMonth ?? FirstOfMonth(_today)).AddMonths(1).AddDays(-1)),
        };

        var min = _firstDate ?? DateOnly.MinValue;
        to = Clamp(to, min, _today);
        from = Clamp(from, min, to);
        var earliest = to.AddDays(1 - DashboardRangeRules.MaxSpanDays);
        if (from < earliest)
        {
            from = Clamp(earliest, min, to);
        }

        return (from, to);
    }

    /// <summary>Adds and removes list entries in place so the selected item stays selected in its dropdown.</summary>
    private void SyncLists()
    {
        var months = new List<DateOnly>();
        var firstMonth = FirstOfMonth(_firstDate ?? _today);
        for (var month = FirstOfMonth(_today); month >= firstMonth; month = month.AddMonths(-1))
        {
            months.Add(month);
        }

        var years = new List<int>();
        for (var year = _today.Year; year >= (_firstDate ?? _today).Year; year--)
        {
            years.Add(year);
        }

        Sync(Months, months);
        Sync(Years, years);
    }

    private static void Sync<T>(ObservableCollection<T> items, List<T> wanted)
    {
        var keep = new HashSet<T>(wanted);
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(items[i]))
            {
                items.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i >= items.Count || !EqualityComparer<T>.Default.Equals(items[i], wanted[i]))
            {
                items.Insert(i, wanted[i]);
            }
        }
    }

    private DateOnly? ClampDay(DateOnly? day) => day is { } d ? Clamp(d, _firstDate ?? DateOnly.MinValue, _today) : null;

    private DateOnly NearestMonth(DateOnly? month) =>
        month is { } m && m < Months[^1] ? Months[^1] : Months[0];

    private int NearestYear(int? year) => year is { } y && y < Years[^1] ? Years[^1] : Years[0];

    private static DateOnly FirstOfMonth(DateOnly day) => new(day.Year, day.Month, 1);

    private static DateOnly Clamp(DateOnly value, DateOnly min, DateOnly max) =>
        value < min ? min : value > max ? max : value;
}

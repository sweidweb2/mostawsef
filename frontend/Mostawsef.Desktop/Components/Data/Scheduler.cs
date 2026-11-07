using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Components;

public enum SchedulerViewMode
{
    Day,
    Week,
}

/// <summary>An entry on the scheduler. Kind colors it like a Badge.</summary>
public class SchedulerItem
{
    public required DateTime Start { get; init; }

    public required DateTime End { get; init; }

    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public BadgeKind Kind { get; init; } = BadgeKind.Primary;

    public object? Tag { get; init; }
}

/// <summary>Day / week time grid for appointments.</summary>
[TemplatePart("PART_DayHeaders", typeof(Grid))]
[TemplatePart("PART_Body", typeof(Grid))]
public class Scheduler : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<SchedulerItem>?> ItemsProperty =
        AvaloniaProperty.Register<Scheduler, IEnumerable<SchedulerItem>?>(nameof(Items));

    public static readonly StyledProperty<DateTime> DateProperty =
        AvaloniaProperty.Register<Scheduler, DateTime>(nameof(Date), DateTime.Today, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<SchedulerViewMode> ViewModeProperty =
        AvaloniaProperty.Register<Scheduler, SchedulerViewMode>(nameof(ViewMode), SchedulerViewMode.Week, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> StartHourProperty =
        AvaloniaProperty.Register<Scheduler, int>(nameof(StartHour), 8);

    public static readonly StyledProperty<int> EndHourProperty =
        AvaloniaProperty.Register<Scheduler, int>(nameof(EndHour), 20);

    public static readonly StyledProperty<double> HourHeightProperty =
        AvaloniaProperty.Register<Scheduler, double>(nameof(HourHeight), 64);

    public static readonly StyledProperty<SchedulerItem?> SelectedItemProperty =
        AvaloniaProperty.Register<Scheduler, SchedulerItem?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<Scheduler, string> TitleProperty =
        AvaloniaProperty.RegisterDirect<Scheduler, string>(nameof(Title), o => o.Title);

    public static readonly DirectProperty<Scheduler, ICommand> PreviousCommandProperty =
        AvaloniaProperty.RegisterDirect<Scheduler, ICommand>(nameof(PreviousCommand), o => o.PreviousCommand);

    public static readonly DirectProperty<Scheduler, ICommand> NextCommandProperty =
        AvaloniaProperty.RegisterDirect<Scheduler, ICommand>(nameof(NextCommand), o => o.NextCommand);

    public static readonly DirectProperty<Scheduler, ICommand> TodayCommandProperty =
        AvaloniaProperty.RegisterDirect<Scheduler, ICommand>(nameof(TodayCommand), o => o.TodayCommand);

    public static readonly DirectProperty<Scheduler, ICommand> DayViewCommandProperty =
        AvaloniaProperty.RegisterDirect<Scheduler, ICommand>(nameof(DayViewCommand), o => o.DayViewCommand);

    public static readonly DirectProperty<Scheduler, ICommand> WeekViewCommandProperty =
        AvaloniaProperty.RegisterDirect<Scheduler, ICommand>(nameof(WeekViewCommand), o => o.WeekViewCommand);

    private const double GutterWidth = 76;

    private Grid? _dayHeaders;
    private Grid? _body;
    private Button? _dayButton;
    private Button? _weekButton;
    private string _title = string.Empty;
    private LocalizationService? _localization;

    public Scheduler()
    {
        PreviousCommand = new RelayCommand(() => Date = Date.AddDays(ViewMode == SchedulerViewMode.Day ? -1 : -7));
        NextCommand = new RelayCommand(() => Date = Date.AddDays(ViewMode == SchedulerViewMode.Day ? 1 : 7));
        TodayCommand = new RelayCommand(() => Date = DateTime.Today);
        DayViewCommand = new RelayCommand(() => ViewMode = SchedulerViewMode.Day);
        WeekViewCommand = new RelayCommand(() => ViewMode = SchedulerViewMode.Week);
    }

    public IEnumerable<SchedulerItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public DateTime Date
    {
        get => GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    public SchedulerViewMode ViewMode
    {
        get => GetValue(ViewModeProperty);
        set => SetValue(ViewModeProperty, value);
    }

    public int StartHour
    {
        get => GetValue(StartHourProperty);
        set => SetValue(StartHourProperty, value);
    }

    public int EndHour
    {
        get => GetValue(EndHourProperty);
        set => SetValue(EndHourProperty, value);
    }

    public double HourHeight
    {
        get => GetValue(HourHeightProperty);
        set => SetValue(HourHeightProperty, value);
    }

    public SchedulerItem? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string Title
    {
        get => _title;
        private set => SetAndRaise(TitleProperty, ref _title, value);
    }

    public ICommand PreviousCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand TodayCommand { get; }

    public ICommand DayViewCommand { get; }

    public ICommand WeekViewCommand { get; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _dayHeaders = e.NameScope.Find<Grid>("PART_DayHeaders");
        _body = e.NameScope.Find<Grid>("PART_Body");
        _dayButton = e.NameScope.Find<Button>("PART_DayButton");
        _weekButton = e.NameScope.Find<Button>("PART_WeekButton");
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _localization = App.Localization;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
            _localization = null;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemsProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldItems)
            {
                oldItems.CollectionChanged -= OnItemsChanged;
            }

            if (change.NewValue is INotifyCollectionChanged newItems)
            {
                newItems.CollectionChanged += OnItemsChanged;
            }

            Rebuild();
        }
        else if (change.Property == DateProperty
                 || change.Property == ViewModeProperty
                 || change.Property == StartHourProperty
                 || change.Property == EndHourProperty
                 || change.Property == HourHeightProperty
                 || change.Property == SelectedItemProperty)
        {
            Rebuild();
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnLanguageChanged(object? sender, EventArgs e) => Rebuild();

    private IReadOnlyList<DateTime> VisibleDays()
    {
        if (ViewMode == SchedulerViewMode.Day)
        {
            return [Date.Date];
        }

        var firstDay = App.Localization.Culture.DateTimeFormat.FirstDayOfWeek;
        var offset = ((int)Date.DayOfWeek - (int)firstDay + 7) % 7;
        var start = Date.Date.AddDays(-offset);
        return Enumerable.Range(0, 7).Select(i => start.AddDays(i)).ToList();
    }

    private void Rebuild()
    {
        var culture = App.Localization.Culture;
        var days = VisibleDays();
        var hours = Math.Max(1, EndHour - StartHour);

        Title = days.Count == 1
            ? days[0].ToString("D", culture)
            : $"{days[0].ToString("d MMM", culture)} - {days[^1].ToString("d MMM yyyy", culture)}";

        _dayButton?.Classes.Set("active", ViewMode == SchedulerViewMode.Day);
        _weekButton?.Classes.Set("active", ViewMode == SchedulerViewMode.Week);

        if (_dayHeaders is null || _body is null)
        {
            return;
        }

        _dayHeaders.Children.Clear();
        _dayHeaders.ColumnDefinitions.Clear();
        _body.Children.Clear();
        _body.ColumnDefinitions.Clear();

        _dayHeaders.ColumnDefinitions.Add(new ColumnDefinition(GutterWidth, GridUnitType.Pixel));
        _body.ColumnDefinitions.Add(new ColumnDefinition(GutterWidth, GridUnitType.Pixel));

        // Hour labels
        var gutter = new StackPanel();
        for (var h = StartHour; h < StartHour + hours; h++)
        {
            gutter.Children.Add(new Border
            {
                Height = HourHeight,
                Classes = { "scheduler-hour-label" },
                Child = new TextBlock { Text = new DateTime(1, 1, 1, h % 24, 0, 0).ToString("t", culture) },
            });
        }

        _body.Children.Add(gutter);

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var column = i + 1;
            _dayHeaders.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            _body.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            var header = new Border
            {
                Classes = { "scheduler-day-header" },
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = culture.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek), Classes = { "caption", "muted" }, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = day.Day.ToString(culture), Classes = { "h4" }, HorizontalAlignment = HorizontalAlignment.Center },
                    },
                },
            };
            if (day == DateTime.Today)
            {
                header.Classes.Add("today");
            }

            Grid.SetColumn(header, column);
            _dayHeaders.Children.Add(header);

            var slots = new StackPanel();
            for (var h = 0; h < hours; h++)
            {
                slots.Children.Add(new Border { Height = HourHeight, Classes = { "scheduler-slot" } });
            }

            var dayPanel = new SchedulerDayPanel { HourHeight = HourHeight, StartHour = StartHour };
            foreach (var item in ItemsForDay(day))
            {
                dayPanel.Children.Add(CreateItemView(item, culture));
            }

            var dayColumn = new Panel { Children = { slots, dayPanel } };
            if (day == DateTime.Today)
            {
                dayColumn.Classes.Add("today");
            }

            dayColumn.Classes.Add("scheduler-day");
            Grid.SetColumn(dayColumn, column);
            _body.Children.Add(dayColumn);
        }
    }

    private IEnumerable<SchedulerItem> ItemsForDay(DateTime day) =>
        (Items ?? []).Where(i => i.Start.Date == day.Date).OrderBy(i => i.Start);

    private Control CreateItemView(SchedulerItem item, System.Globalization.CultureInfo culture)
    {
        var view = new Border
        {
            Classes = { "scheduler-item", item.Kind.ToString().ToLowerInvariant() },
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = item.Title, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"{item.Start.ToString("t", culture)} - {item.End.ToString("t", culture)}", Classes = { "caption" } },
                    new TextBlock { Text = item.Subtitle, Classes = { "caption" }, IsVisible = !string.IsNullOrEmpty(item.Subtitle), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis },
                },
            },
        };

        if (ReferenceEquals(item, SelectedItem))
        {
            view.Classes.Add("selected");
        }

        ToolTip.SetTip(view, item.Title);
        view.PointerPressed += (_, _) => SelectedItem = item;
        SchedulerDayPanel.SetItem(view, item);
        return view;
    }
}

/// <summary>Positions scheduler entries vertically by time; overlapping entries share the width.</summary>
public class SchedulerDayPanel : Panel
{
    public static readonly AttachedProperty<SchedulerItem?> ItemProperty =
        AvaloniaProperty.RegisterAttached<SchedulerDayPanel, Control, SchedulerItem?>("Item");

    public double HourHeight { get; set; } = 64;

    public int StartHour { get; set; } = 8;

    public static void SetItem(Control control, SchedulerItem? item) => control.SetValue(ItemProperty, item);

    public static SchedulerItem? GetItem(Control control) => control.GetValue(ItemProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }

        return new Size(0, 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var placed = Children
            .Select(c => (Control: c, Item: GetItem(c)))
            .Where(x => x.Item is not null)
            .OrderBy(x => x.Item!.Start)
            .ToList();

        var laneEnds = new List<DateTime>();
        var lanes = new Dictionary<Control, int>();
        foreach (var (control, item) in placed)
        {
            var lane = laneEnds.FindIndex(end => end <= item!.Start);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(item!.End);
            }
            else
            {
                laneEnds[lane] = item!.End;
            }

            lanes[control] = lane;
        }

        var laneCount = Math.Max(1, laneEnds.Count);
        var laneWidth = finalSize.Width / laneCount;

        foreach (var (control, item) in placed)
        {
            var top = (item!.Start.TimeOfDay.TotalHours - StartHour) * HourHeight;
            var height = Math.Max(24, (item.End - item.Start).TotalHours * HourHeight - 2);
            control.Arrange(new Rect(lanes[control] * laneWidth + 2, top + 1, Math.Max(0, laneWidth - 4), height));
        }

        return finalSize;
    }
}

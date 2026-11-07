using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Components;

/// <summary>Page navigation. CurrentPage is 1-based and clamped to 1..PageCount.</summary>
public class Pagination : TemplatedControl
{
    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<Pagination, int>(nameof(CurrentPage), 1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> PageCountProperty =
        AvaloniaProperty.Register<Pagination, int>(nameof(PageCount), 1);

    public static readonly DirectProperty<Pagination, string> SummaryProperty =
        AvaloniaProperty.RegisterDirect<Pagination, string>(nameof(Summary), o => o.Summary);

    public static readonly DirectProperty<Pagination, ICommand> FirstCommandProperty =
        AvaloniaProperty.RegisterDirect<Pagination, ICommand>(nameof(FirstCommand), o => o.FirstCommand);

    public static readonly DirectProperty<Pagination, ICommand> PreviousCommandProperty =
        AvaloniaProperty.RegisterDirect<Pagination, ICommand>(nameof(PreviousCommand), o => o.PreviousCommand);

    public static readonly DirectProperty<Pagination, ICommand> NextCommandProperty =
        AvaloniaProperty.RegisterDirect<Pagination, ICommand>(nameof(NextCommand), o => o.NextCommand);

    public static readonly DirectProperty<Pagination, ICommand> LastCommandProperty =
        AvaloniaProperty.RegisterDirect<Pagination, ICommand>(nameof(LastCommand), o => o.LastCommand);

    private readonly RelayCommand _first;
    private readonly RelayCommand _previous;
    private readonly RelayCommand _next;
    private readonly RelayCommand _last;
    private string _summary = string.Empty;
    private LocalizationService? _localization;

    public Pagination()
    {
        _first = new RelayCommand(() => CurrentPage = 1, () => CurrentPage > 1);
        _previous = new RelayCommand(() => CurrentPage--, () => CurrentPage > 1);
        _next = new RelayCommand(() => CurrentPage++, () => CurrentPage < PageCount);
        _last = new RelayCommand(() => CurrentPage = PageCount, () => CurrentPage < PageCount);
        Refresh();
    }

    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public int PageCount
    {
        get => GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetAndRaise(SummaryProperty, ref _summary, value);
    }

    public ICommand FirstCommand => _first;

    public ICommand PreviousCommand => _previous;

    public ICommand NextCommand => _next;

    public ICommand LastCommand => _last;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _localization = App.Localization;
        _localization.LanguageChanged += OnLanguageChanged;
        Refresh();
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

        if (change.Property == CurrentPageProperty || change.Property == PageCountProperty)
        {
            var clamped = Math.Clamp(CurrentPage, 1, Math.Max(1, PageCount));
            if (clamped != CurrentPage)
            {
                CurrentPage = clamped;
                return;
            }

            Refresh();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        Summary = App.Localization.Format("Pagination.Summary", CurrentPage, Math.Max(1, PageCount));
        _first.NotifyCanExecuteChanged();
        _previous.NotifyCanExecuteChanged();
        _next.NotifyCanExecuteChanged();
        _last.NotifyCanExecuteChanged();
    }
}

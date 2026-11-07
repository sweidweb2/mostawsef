using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Frame for a DataGrid (Content): optional Toolbar and Footer (e.g. Pagination),
/// plus loading and empty states.
/// </summary>
public class DataTable : ContentControl
{
    public static readonly StyledProperty<object?> ToolbarProperty =
        AvaloniaProperty.Register<DataTable, object?>(nameof(Toolbar));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<DataTable, object?>(nameof(Footer));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<DataTable, bool>(nameof(IsLoading));

    public static readonly StyledProperty<bool> IsEmptyProperty =
        AvaloniaProperty.Register<DataTable, bool>(nameof(IsEmpty));

    public static readonly StyledProperty<string?> EmptyTitleProperty =
        AvaloniaProperty.Register<DataTable, string?>(nameof(EmptyTitle));

    public static readonly StyledProperty<string?> EmptyDescriptionProperty =
        AvaloniaProperty.Register<DataTable, string?>(nameof(EmptyDescription));

    public static readonly DirectProperty<DataTable, bool> ShowEmptyProperty =
        AvaloniaProperty.RegisterDirect<DataTable, bool>(nameof(ShowEmpty), o => o.ShowEmpty);

    private bool _showEmpty;

    public object? Toolbar
    {
        get => GetValue(ToolbarProperty);
        set => SetValue(ToolbarProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public bool IsEmpty
    {
        get => GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }

    public string? EmptyTitle
    {
        get => GetValue(EmptyTitleProperty);
        set => SetValue(EmptyTitleProperty, value);
    }

    public string? EmptyDescription
    {
        get => GetValue(EmptyDescriptionProperty);
        set => SetValue(EmptyDescriptionProperty, value);
    }

    public bool ShowEmpty
    {
        get => _showEmpty;
        private set => SetAndRaise(ShowEmptyProperty, ref _showEmpty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEmptyProperty || change.Property == IsLoadingProperty)
        {
            ShowEmpty = IsEmpty && !IsLoading;
        }
    }
}

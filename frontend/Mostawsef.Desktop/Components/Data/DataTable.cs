using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Frame for a DataGrid (Content): optional Toolbar and Footer (e.g. Pagination),
/// plus loading, empty and error states. The error state wins over the empty state.
/// A "#" column of <see cref="RowNumber"/> cells numbers the rows from <see cref="FirstRowNumber"/>.
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

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<DataTable, bool>(nameof(HasError));

    public static readonly StyledProperty<string?> ErrorTitleProperty =
        AvaloniaProperty.Register<DataTable, string?>(nameof(ErrorTitle));

    public static readonly StyledProperty<string?> ErrorDescriptionProperty =
        AvaloniaProperty.Register<DataTable, string?>(nameof(ErrorDescription));

    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<DataTable, ICommand?>(nameof(RetryCommand));

    public static readonly StyledProperty<int> FirstRowNumberProperty =
        AvaloniaProperty.Register<DataTable, int>(nameof(FirstRowNumber), 1);

    public static readonly DirectProperty<DataTable, bool> ShowEmptyProperty =
        AvaloniaProperty.RegisterDirect<DataTable, bool>(nameof(ShowEmpty), o => o.ShowEmpty);

    public static readonly DirectProperty<DataTable, bool> ShowErrorProperty =
        AvaloniaProperty.RegisterDirect<DataTable, bool>(nameof(ShowError), o => o.ShowError);

    private bool _showEmpty;
    private bool _showError;

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

    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public string? ErrorTitle
    {
        get => GetValue(ErrorTitleProperty);
        set => SetValue(ErrorTitleProperty, value);
    }

    public string? ErrorDescription
    {
        get => GetValue(ErrorDescriptionProperty);
        set => SetValue(ErrorDescriptionProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    /// <summary>Number shown by <see cref="RowNumber"/> cells on the first row (e.g. 41 on page 3 of 20 per page).</summary>
    public int FirstRowNumber
    {
        get => GetValue(FirstRowNumberProperty);
        set => SetValue(FirstRowNumberProperty, value);
    }

    public bool ShowEmpty
    {
        get => _showEmpty;
        private set => SetAndRaise(ShowEmptyProperty, ref _showEmpty, value);
    }

    public bool ShowError
    {
        get => _showError;
        private set => SetAndRaise(ShowErrorProperty, ref _showError, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEmptyProperty || change.Property == IsLoadingProperty || change.Property == HasErrorProperty)
        {
            ShowError = HasError && !IsLoading;
            ShowEmpty = IsEmpty && !IsLoading && !HasError;
        }
    }
}

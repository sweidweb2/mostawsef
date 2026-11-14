using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// <c>ui:SelectionColumn.IsEnabled="True"</c> on a DataGrid whose DataContext is an <see cref="ISelectableList"/>: the
/// grid's first column (the row checkboxes) is shown only while the list is in select mode. Columns are not in the
/// visual tree, so they cannot bind to the page's DataContext themselves.
/// </summary>
public static class SelectionColumn
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<DataGrid, bool>("IsEnabled", typeof(SelectionColumn));

    private static readonly AttachedProperty<Tracker?> TrackerProperty =
        AvaloniaProperty.RegisterAttached<DataGrid, Tracker?>("Tracker", typeof(SelectionColumn));

    static SelectionColumn()
    {
        IsEnabledProperty.Changed.AddClassHandler<DataGrid>((grid, e) =>
        {
            grid.GetValue(TrackerProperty)?.Dispose();
            grid.SetValue(TrackerProperty, e.NewValue is true ? new Tracker(grid) : null);
        });
    }

    public static bool GetIsEnabled(DataGrid grid) => grid.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DataGrid grid, bool value) => grid.SetValue(IsEnabledProperty, value);

    private sealed class Tracker : IDisposable
    {
        private readonly DataGrid _grid;
        private RowSelection? _selection;

        public Tracker(DataGrid grid)
        {
            _grid = grid;
            grid.DataContextChanged += OnDataContextChanged;
            Track();
        }

        public void Dispose()
        {
            _grid.DataContextChanged -= OnDataContextChanged;
            Untrack();
        }

        private void OnDataContextChanged(object? sender, EventArgs e) => Track();

        private void Track()
        {
            Untrack();
            _selection = (_grid.DataContext as ISelectableList)?.Selection;
            if (_selection is not null)
            {
                _selection.PropertyChanged += OnSelectionChanged;
            }

            Apply();
        }

        private void Untrack()
        {
            if (_selection is not null)
            {
                _selection.PropertyChanged -= OnSelectionChanged;
                _selection = null;
            }
        }

        private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RowSelection.IsActive))
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_grid.Columns.Count > 0)
            {
                _grid.Columns[0].IsVisible = _selection?.IsActive == true;
            }
        }
    }
}

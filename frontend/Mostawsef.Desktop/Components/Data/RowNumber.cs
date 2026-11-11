using System;
using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Cell content of a "#" column: the row's position in the DataGrid's items plus the enclosing
/// DataTable's FirstRowNumber, so numbering continues across pages.
/// </summary>
public class RowNumber : TextBlock
{
    private DataTable? _table;
    private DataGrid? _grid;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _table = this.FindAncestorOfType<DataTable>();
        _grid = this.FindAncestorOfType<DataGrid>();
        if (_table is not null)
        {
            _table.PropertyChanged += OnSourceChanged;
        }

        if (_grid is not null)
        {
            _grid.PropertyChanged += OnSourceChanged;
        }

        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_table is not null)
        {
            _table.PropertyChanged -= OnSourceChanged;
        }

        if (_grid is not null)
        {
            _grid.PropertyChanged -= OnSourceChanged;
        }

        _table = null;
        _grid = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Refresh();
    }

    private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DataTable.FirstRowNumberProperty || e.Property == DataGrid.ItemsSourceProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var index = DataContext is null || _grid?.ItemsSource is not IList items ? -1 : items.IndexOf(DataContext);
        var next = index < 0 ? string.Empty : ((_table?.FirstRowNumber ?? 1) + index).ToString(CultureInfo.InvariantCulture);
        if (Text != next)
        {
            Text = next;
        }
    }
}

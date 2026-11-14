using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Cell content of a "#" column: the row's position in the DataGrid plus the enclosing DataTable's FirstRowNumber,
/// so numbering continues across pages. The position is the DataGrid row's own index, which the grid keeps right
/// when rows are recycled while scrolling or shift when the list changes; the items lookup below is only the fallback
/// for a cell that is not (yet) inside a row.
/// </summary>
public class RowNumber : TextBlock
{
    private DataTable? _table;
    private DataGrid? _grid;
    private DataGridRow? _row;
    private INotifyCollectionChanged? _items;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _table = this.FindAncestorOfType<DataTable>();
        _grid = this.FindAncestorOfType<DataGrid>();
        _row = this.FindAncestorOfType<DataGridRow>();
        if (_table is not null)
        {
            _table.PropertyChanged += OnSourceChanged;
        }

        if (_grid is not null)
        {
            _grid.PropertyChanged += OnSourceChanged;
            AttachItems(_grid.ItemsSource);
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

        DetachItems();
        _table = null;
        _grid = null;
        _row = null;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Refresh();
    }

    private void OnSourceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DataGrid.ItemsSourceProperty)
        {
            AttachItems(e.NewValue);
            Refresh();
            return;
        }

        if (e.Property == DataTable.FirstRowNumberProperty)
        {
            Refresh();
        }
    }

    private void AttachItems(object? source)
    {
        DetachItems();
        if (source is IList list)
        {
            // Watch the list before this cell subscribes, so the shared map goes dirty first.
            RowIndexLookup.Watch(list);
        }

        if (source is INotifyCollectionChanged notify)
        {
            _items = notify;
            _items.CollectionChanged += OnItemsChanged;
        }
    }

    private void DetachItems()
    {
        if (_items is null)
        {
            return;
        }

        _items.CollectionChanged -= OnItemsChanged;
        _items = null;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Refresh();

        // The grid updates its row indexes in its own change handler, which may run after this one: look again once it has.
        Dispatcher.UIThread.Post(Refresh, DispatcherPriority.Loaded);
    }

    private void Refresh()
    {
        var index = _row is { Index: >= 0 } row ? row.Index : -1;
        if (index < 0 && DataContext is not null && _grid?.ItemsSource is IList items)
        {
            index = RowIndexLookup.IndexOf(items, DataContext);
        }

        var next = index < 0 ? string.Empty : ((_table?.FirstRowNumber ?? 1) + index).ToString(CultureInfo.InvariantCulture);
        if (Text != next)
        {
            Text = next;
        }
    }
}

/// <summary>Shared O(1) item→index map per list instance (all RowNumber cells reuse it).</summary>
file static class RowIndexLookup
{
    private static readonly ConditionalWeakTable<object, Entry> Cache = [];

    public static void Watch(IList items) => Cache.GetValue(items, static key => new Entry((IList)key));

    public static int IndexOf(IList items, object item)
    {
        var entry = Cache.GetValue(items, static key => new Entry((IList)key));
        return entry.IndexOf(item);
    }

    private sealed class Entry
    {
        private readonly IList _items;
        private Dictionary<object, int>? _map;
        private bool _dirty = true;

        public Entry(IList items)
        {
            _items = items;
            if (items is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += (_, _) => _dirty = true;
            }
        }

        public int IndexOf(object item)
        {
            EnsureMap();
            if (_map!.TryGetValue(item, out var index)
                && index < _items.Count
                && Equals(_items[index], item))
            {
                return index;
            }

            // DataGrid may materialize a row before our CollectionChanged dirty flag runs.
            _dirty = true;
            EnsureMap();
            return _map.TryGetValue(item, out index) ? index : -1;
        }

        private void EnsureMap()
        {
            if (!_dirty && _map is not null)
            {
                return;
            }

            _map ??= new Dictionary<object, int>(_items.Count);
            _map.Clear();
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i] is { } row)
                {
                    _map.TryAdd(row, i);
                }
            }

            _dirty = false;
        }
    }
}

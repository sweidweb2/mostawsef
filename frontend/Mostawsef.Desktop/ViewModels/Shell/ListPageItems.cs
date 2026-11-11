using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Rows of a list page. Keeps one collection instance for the DataGrid (no ItemsSource swap) and
/// replaces the page with a single Reset notification instead of Clear + N Adds (which rebuild twice).
/// </summary>
public sealed class ListPageItems<T> : ObservableCollection<T>
{
    /// <summary>Replaces every row. No-op when the sequence is already the same (record/value equality).</summary>
    public void ReplaceAll(IReadOnlyList<T> items)
    {
        if (SameAs(items))
        {
            return;
        }

        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private bool SameAs(IReadOnlyList<T> items)
    {
        if (items.Count != Count)
        {
            return false;
        }

        var comparer = EqualityComparer<T>.Default;
        for (var i = 0; i < Count; i++)
        {
            if (!comparer.Equals(this[i], items[i]))
            {
                return false;
            }
        }

        return true;
    }
}

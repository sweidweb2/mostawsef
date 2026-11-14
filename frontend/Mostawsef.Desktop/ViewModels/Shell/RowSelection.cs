using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Checkbox selection of list rows by id, kept across pages. <see cref="IsActive"/> is the "select mode" that shows the
/// checkbox column. Rows stay plain DTOs: a row checkbox binds <see cref="IsSelectedConverter"/> to
/// (Selection, Selection.Version, row Id), and <see cref="Version"/> changes on every edit so those bindings refresh.
/// </summary>
public partial class RowSelection : ObservableObject
{
    private readonly HashSet<Guid> _ids = [];

    /// <summary>True while the checkbox column and the bulk actions are shown. Leaving select mode clears the selection.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>Bumped on every change; bind it next to the row id so checkboxes re-read their state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count), nameof(HasAny))]
    public partial int Version { get; private set; }

    public int Count => _ids.Count;

    public bool HasAny => _ids.Count > 0;

    public IReadOnlyList<Guid> Ids => _ids.ToList();

    /// <summary>(RowSelection, Version, Guid id) → is that id selected.</summary>
    public static IMultiValueConverter IsSelectedConverter { get; } = new SelectedConverter();

    public bool Contains(Guid id) => _ids.Contains(id);

    public void Toggle(Guid id)
    {
        if (!_ids.Remove(id))
        {
            _ids.Add(id);
        }

        Version++;
    }

    public void Set(IEnumerable<Guid> ids, bool selected)
    {
        var changed = false;
        foreach (var id in ids)
        {
            changed |= selected ? _ids.Add(id) : _ids.Remove(id);
        }

        if (changed)
        {
            Version++;
        }
    }

    public void Clear()
    {
        if (_ids.Count == 0)
        {
            return;
        }

        _ids.Clear();
        Version++;
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (!value)
        {
            Clear();
        }
    }

    private sealed class SelectedConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            values.Count >= 3 && values[0] is RowSelection selection && values[2] is Guid id && selection.Contains(id);
    }
}

/// <summary>A list page whose rows can be checked (see <see cref="RowSelection"/>).</summary>
public interface ISelectableList
{
    RowSelection Selection { get; }
}

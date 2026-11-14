using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Mostawsef.Desktop.Components;

/// <summary>Finds the record behind a tap on a list row (a DataGrid row or a <see cref="Card"/>).</summary>
public static class RecordTap
{
    /// <summary>
    /// The row's or card's data, or null when the tap landed on a button inside it (edit, delete, copy keep their own click)
    /// or outside any row.
    /// </summary>
    public static object? RecordOf(TappedEventArgs e) =>
        e.Source is not Visual source || source.FindAncestorOfType<Button>() is not null
            ? null
            : source.FindAncestorOfType<DataGridRow>()?.DataContext ?? source.FindAncestorOfType<Card>()?.DataContext;
}

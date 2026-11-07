using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Lays children out in equal columns; the column count adapts to the available width.
/// Set ResponsiveGrid.ColumnSpan on a child to span columns (0 = full row).
/// </summary>
public class ResponsiveGrid : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<ResponsiveGrid, double>(nameof(MinColumnWidth), 260);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<ResponsiveGrid, int>(nameof(MaxColumns), 4);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<ResponsiveGrid, double>(nameof(ColumnSpacing), 16);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<ResponsiveGrid, double>(nameof(RowSpacing), 16);

    public static readonly AttachedProperty<int> ColumnSpanProperty =
        AvaloniaProperty.RegisterAttached<ResponsiveGrid, Control, int>("ColumnSpan", 1);

    static ResponsiveGrid()
    {
        AffectsMeasure<ResponsiveGrid>(MinColumnWidthProperty, MaxColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
        AffectsParentMeasure<ResponsiveGrid>(ColumnSpanProperty);
    }

    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public static int GetColumnSpan(Control control) => control.GetValue(ColumnSpanProperty);

    public static void SetColumnSpan(Control control, int value) => control.SetValue(ColumnSpanProperty, value);

    public int ColumnCount { get; private set; } = 1;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth * MaxColumns : availableSize.Width;
        ColumnCount = CalculateColumns(width);
        var columnWidth = ColumnWidth(width);

        double totalHeight = 0, rowHeight = 0;
        var column = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            var span = Span(child);
            if (column + span > ColumnCount)
            {
                totalHeight += rowHeight + RowSpacing;
                rowHeight = 0;
                column = 0;
            }

            child.Measure(new Size(columnWidth * span + ColumnSpacing * (span - 1), double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            column += span;
        }

        totalHeight += rowHeight;
        return new Size(width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ColumnCount = CalculateColumns(finalSize.Width);
        var columnWidth = ColumnWidth(finalSize.Width);

        double y = 0, rowHeight = 0;
        var column = 0;
        var rowStart = 0;
        var children = Children;

        for (var i = 0; i <= children.Count; i++)
        {
            var endOfChildren = i == children.Count;
            var child = endOfChildren ? null : children[i];

            if (child is { IsVisible: false })
            {
                continue;
            }

            var span = child is null ? 0 : Span(child);
            if (endOfChildren || column + span > ColumnCount)
            {
                ArrangeRow(rowStart, i, y, rowHeight, columnWidth);
                y += rowHeight + RowSpacing;
                rowHeight = 0;
                column = 0;
                rowStart = i;
            }

            if (child is not null)
            {
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                column += span;
            }
        }

        return finalSize;
    }

    private void ArrangeRow(int start, int end, double y, double height, double columnWidth)
    {
        var column = 0;
        for (var i = start; i < end; i++)
        {
            var child = Children[i];
            if (!child.IsVisible)
            {
                continue;
            }

            var span = Span(child);
            var x = column * (columnWidth + ColumnSpacing);
            var width = columnWidth * span + ColumnSpacing * (span - 1);
            var childHeight = child.VerticalAlignment == VerticalAlignment.Stretch ? height : child.DesiredSize.Height;
            child.Arrange(new Rect(x, y, width, childHeight));
            column += span;
        }
    }

    private int CalculateColumns(double width)
    {
        var columns = (int)Math.Floor((width + ColumnSpacing) / (MinColumnWidth + ColumnSpacing));
        return Math.Clamp(columns, 1, Math.Max(1, MaxColumns));
    }

    private double ColumnWidth(double width) =>
        Math.Max(0, (width - ColumnSpacing * (ColumnCount - 1)) / ColumnCount);

    private int Span(Control child)
    {
        var span = GetColumnSpan(child);
        return span <= 0 ? ColumnCount : Math.Min(span, ColumnCount);
    }
}

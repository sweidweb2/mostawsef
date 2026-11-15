using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

namespace Mostawsef.Desktop.Components;

/// <summary>What one time point of a <see cref="LineChart"/> stands for: its hour, day or month.</summary>
public enum ChartUnit
{
    Day,
    Hour,
    Month,
}

/// <summary>
/// One value of a <see cref="LineChart"/>: a count with either a moment (<see cref="Time"/>: the start of its hour, day
/// or month, written in the app's language when drawn) or a plain text label such as a test name.
/// </summary>
public sealed record ChartPoint(string Label, int Value, DateTime? Time = null, ChartUnit Unit = ChartUnit.Day)
{
    public static ChartPoint Day(DateOnly date, int value) => new(string.Empty, value, date.ToDateTime(TimeOnly.MinValue));

    public static ChartPoint At(DateTime start, ChartUnit unit, int value) => new(string.Empty, value, start, unit);
}

/// <summary>
/// A line graph of counts: whole-number gridlines on the left, labels along the bottom, the line, its dots and a soft
/// fill. Time points (hours, days or months) are spread with about eight labels; named points (e.g. tests) all get a label, slanted so long names
/// fit, and cut with "…" when very long (the tooltip has the full name). Hovering a dot shows its label and count.
/// Colors come from the theme, so it follows light/dark. Text is drawn left-to-right in both languages.
/// </summary>
public class LineChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<ChartPoint>?>(nameof(Points));

    /// <summary>The tooltip text of a point: "{0}" is its day or name, "{1}" the count.</summary>
    public static readonly StyledProperty<string?> TooltipFormatProperty =
        AvaloniaProperty.Register<LineChart, string?>(nameof(TooltipFormat), "{0}: {1}");

    /// <summary>The line, dot and fill color; the theme's primary brush when not set.</summary>
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<LineChart, IBrush?>(nameof(Stroke));

    private const double MinLeftMargin = 36;
    private const double RightMargin = 12;
    private const double TopMargin = 12;
    private const double DayBottomMargin = 28;
    private const int GridLines = 4;
    private const int MaxLabelLength = 18;
    private const double Slant = 0.7071067811865476; // sin and cos of 45 degrees

    private int _hover = -1;

    static LineChart()
    {
        AffectsRender<LineChart>(PointsProperty, StrokeProperty);
        AffectsMeasure<LineChart>(PointsProperty);
    }

    public LineChart()
    {
        MinHeight = 200;
        ClipToBounds = true;
    }

    public IReadOnlyList<ChartPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public string? TooltipFormat
    {
        get => GetValue(TooltipFormatProperty);
        set => SetValue(TooltipFormatProperty, value);
    }

    private static CultureInfo Culture => App.Services is null ? CultureInfo.CurrentCulture : App.Localization.Culture;

    /// <summary>The top of the y axis: a multiple of the gridline count, so every gridline is a whole number.</summary>
    public static int AxisMaximum(int largest) => Math.Max(1, (int)Math.Ceiling(largest / (double)GridLines)) * GridLines;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (App.Services is not null)
        {
            App.Localization.LanguageChanged += OnLanguageChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (App.Services is not null)
        {
            App.Localization.LanguageChanged -= OnLanguageChanged;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => InvalidateVisual();

    private IBrush Themed(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : fallback;

    private static bool IsNamed(IReadOnlyList<ChartPoint> points) => points.Any(p => p.Time is null);

    private static string AxisLabel(ChartPoint point) => point.Time switch
    {
        null => Shorten(point.Label),
        { } time when point.Unit == ChartUnit.Hour => time.ToString("HH:mm", CultureInfo.InvariantCulture),
        { } time when point.Unit == ChartUnit.Month => time.ToString("MMM yyyy", Culture),
        { } time => time.ToString("MMM d", Culture),
    };

    private static string Shorten(string label) =>
        label.Length <= MaxLabelLength ? label : label[..(MaxLabelLength - 1)].TrimEnd() + "…";

    private static string FullLabel(ChartPoint point) => point.Time switch
    {
        null => point.Label,
        { } time when point.Unit == ChartUnit.Hour =>
            time.ToString("D", Culture) + " " + time.ToString("HH:mm", CultureInfo.InvariantCulture),
        { } time when point.Unit == ChartUnit.Month => time.ToString("MMMM yyyy", Culture),
        { } time => time.ToString("D", Culture),
    };

    private FormattedText Text(string text, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(TextElement.GetFontFamily(this)), 11, brush);

    /// <summary>The drawing area inside the axes. Named points need room below and to the left for their slanted labels.</summary>
    private Rect PlotArea(IReadOnlyList<ChartPoint>? points)
    {
        // Room for the widest gridline label (the top one), e.g. large money totals.
        var top = AxisMaximum(points is { Count: > 0 } ? points.Max(p => p.Value) : 0);
        var left = Math.Max(MinLeftMargin, Text(top.ToString(CultureInfo.InvariantCulture), Brushes.Black).Width + 14);
        var bottom = DayBottomMargin;
        if (points is { Count: > 0 } && IsNamed(points))
        {
            var widest = points.Max(p => Text(AxisLabel(p), Brushes.Black).Width);
            var first = Text(AxisLabel(points[0]), Brushes.Black).Width;
            bottom = Math.Min(Bounds.Height * 0.5, widest * Slant + 18);
            left = Math.Min(Bounds.Width * 0.3, Math.Max(left, first * Slant + 10));
        }

        return new Rect(left, TopMargin, Math.Max(1, Bounds.Width - left - RightMargin), Math.Max(1, Bounds.Height - TopMargin - bottom));
    }

    private static double XOf(int index, int count, Rect plot) =>
        count <= 1 ? plot.Center.X : plot.Left + plot.Width * index / (count - 1);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var points = Points;
        var plot = PlotArea(points);
        var line = Stroke ?? Themed("BrushPrimary", Brushes.SteelBlue);
        var grid = Themed("BrushDivider", Brushes.LightGray);
        var muted = Themed("BrushTextMuted", Brushes.Gray);

        var top = AxisMaximum(points is { Count: > 0 } ? points.Max(p => p.Value) : 0);
        var gridPen = new Pen(grid, 1);

        for (var i = 0; i <= GridLines; i++)
        {
            var y = plot.Bottom - plot.Height * i / GridLines;
            context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var label = Text((top * i / GridLines).ToString(CultureInfo.InvariantCulture), muted);
            context.DrawText(label, new Point(plot.Left - 8 - label.Width, y - label.Height / 2));
        }

        if (points is not { Count: > 0 })
        {
            return;
        }

        var coordinates = new Point[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            coordinates[i] = new Point(XOf(i, points.Count, plot), plot.Bottom - plot.Height * points[i].Value / top);
        }

        // Soft fill under the line, then the line itself.
        var fill = new StreamGeometry();
        using (var g = fill.Open())
        {
            g.BeginFigure(new Point(coordinates[0].X, plot.Bottom), isFilled: true);
            foreach (var point in coordinates)
            {
                g.LineTo(point);
            }

            g.LineTo(new Point(coordinates[^1].X, plot.Bottom));
            g.EndFigure(isClosed: true);
        }

        var fillBrush = line is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.12) : line;
        context.DrawGeometry(fillBrush, null, fill);

        var path = new StreamGeometry();
        using (var g = path.Open())
        {
            g.BeginFigure(coordinates[0], isFilled: false);
            for (var i = 1; i < coordinates.Length; i++)
            {
                g.LineTo(coordinates[i]);
            }

            g.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, new Pen(line, 2.5, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round), path);

        var surface = Themed("BrushSurface", Brushes.White);
        for (var i = 0; i < coordinates.Length; i++)
        {
            var radius = i == _hover ? 6 : 3.5;
            context.DrawEllipse(i == _hover ? line : surface, new Pen(line, 2), coordinates[i], radius, radius);
        }

        if (IsNamed(points))
        {
            for (var i = 0; i < points.Count; i++)
            {
                DrawSlanted(context, AxisLabel(points[i]), coordinates[i].X, plot.Bottom + 8, i == _hover ? line : muted);
            }

            return;
        }

        // Time points: about eight labels, always including the first and the last.
        var step = Math.Max(1, (int)Math.Ceiling(points.Count / 8.0));
        for (var i = 0; i < points.Count; i += step)
        {
            if (points.Count - 1 - i < step && i != points.Count - 1 && step > 1)
            {
                continue;
            }

            DrawDate(context, AxisLabel(points[i]), coordinates[i].X, plot, muted);
        }

        if ((points.Count - 1) % step != 0)
        {
            DrawDate(context, AxisLabel(points[^1]), coordinates[^1].X, plot, muted);
        }
    }

    private void DrawDate(DrawingContext context, string text, double x, Rect plot, IBrush brush)
    {
        var label = Text(text, brush);
        var left = Math.Clamp(x - label.Width / 2, 0, Math.Max(0, Bounds.Width - label.Width));
        context.DrawText(label, new Point(left, plot.Bottom + 8));
    }

    /// <summary>A label tilted 45 degrees upwards that ends at its dot's column.</summary>
    private void DrawSlanted(DrawingContext context, string text, double x, double y, IBrush brush)
    {
        var label = Text(text, brush);
        var transform = Matrix.CreateRotation(-Math.PI / 4) * Matrix.CreateTranslation(x, y);
        using (context.PushTransform(transform))
        {
            context.DrawText(label, new Point(-label.Width, 0));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var points = Points;
        if (points is not { Count: > 0 })
        {
            return;
        }

        var plot = PlotArea(points);
        var x = e.GetPosition(this).X;
        var nearest = 0;
        for (var i = 1; i < points.Count; i++)
        {
            if (Math.Abs(XOf(i, points.Count, plot) - x) < Math.Abs(XOf(nearest, points.Count, plot) - x))
            {
                nearest = i;
            }
        }

        if (nearest == _hover)
        {
            return;
        }

        _hover = nearest;
        var point = points[nearest];
        ToolTip.SetTip(this, string.Format(Culture, TooltipFormat ?? "{0}: {1}", FullLabel(point), point.Value));
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        ToolTip.SetTip(this, null);
        InvalidateVisual();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Views;

/// <summary>Every table's rows: their height and the hover tint with the start-edge accent bar.</summary>
public class TableRowStyleTests
{
    [AvaloniaFact]
    public async Task Rows_are_tall_and_hovering_one_tints_it_and_shows_the_accent_bar()
    {
        using var app = new TestServices();
        var grid = new DataGrid
        {
            ItemsSource = new List<string> { "first", "second" },
            Columns = { new DataGridTextColumn { Header = "Name", Width = new DataGridLength(1, DataGridLengthUnitType.Star) } },
        };
        var window = app.Show(grid);
        Dispatcher.UIThread.RunJobs();
        var rows = window.GetVisualDescendants().OfType<DataGridRow>().OrderBy(r => r.Bounds.Y).ToList();

        Assert.All(rows, row => Assert.InRange(row.Bounds.Height, Resource<double>(window, "TableRowHeight"), Resource<double>(window, "TableRowHeight") + 1));
        Assert.Equal(Resource<Thickness>(window, "TableRowAccent"), Part<Border>(rows[0], "RowBorder").BorderThickness);
        Assert.Equal(Colors.Transparent, Color(Part<Border>(rows[0], "RowBorder").BorderBrush));

        window.MouseMove(rows[0].TranslatePoint(new Point(rows[0].Bounds.Width / 2, rows[0].Bounds.Height / 2), window)!.Value);
        await Settle();

        Assert.Equal(Color(Resource<IBrush>(window, "BrushPrimary")), Color(Part<Border>(rows[0], "RowBorder").BorderBrush));
        Assert.Equal(Color(Resource<IBrush>(window, "BrushPrimarySubtle")), Color(Part<Rectangle>(rows[0], "BackgroundRectangle").Fill));
        Assert.Equal(Colors.Transparent, Color(Part<Border>(rows[1], "RowBorder").BorderBrush));
        Assert.Equal(Part<Border>(rows[0], "RowBorder").BorderThickness, Part<Border>(rows[1], "RowBorder").BorderThickness);
    }

    private static T Part<T>(DataGridRow row, string name) where T : Control =>
        row.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    /// <summary>Looked up from the window, so theme brushes resolve for its Light/Dark variant.</summary>
    private static T Resource<T>(Window window, string key) =>
        window.TryFindResource(key, window.ActualThemeVariant, out var value) ? (T)value! : throw new KeyNotFoundException(key);

    private static Color? Color(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>Lets the short hover transitions (DurationFast) finish: they run on the real clock.</summary>
    private static async Task Settle()
    {
        var until = DateTime.UtcNow.AddMilliseconds(400);
        while (DateTime.UtcNow < until)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(16, TestContext.Current.CancellationToken);
        }

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}

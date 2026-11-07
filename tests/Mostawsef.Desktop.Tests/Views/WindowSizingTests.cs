using Avalonia;
using Mostawsef.Desktop.Views;

namespace Mostawsef.Desktop.Tests.Views;

public class WindowSizingTests
{
    [Fact]
    public void Restored_window_keeps_its_preferred_size_on_a_large_screen()
    {
        var size = WindowSizing.FitToWorkArea(new Size(1280, 820), new Size(1920, 1040));

        Assert.Equal(new Size(1280, 820), size);
    }

    [Fact]
    public void Restored_window_shrinks_to_fit_a_small_screen()
    {
        var size = WindowSizing.FitToWorkArea(new Size(1280, 820), new Size(1280, 680));

        Assert.True(size.Width < 1280);
        Assert.True(size.Height < 680);
        Assert.Equal(new Size(1152, 612), size);
    }
}

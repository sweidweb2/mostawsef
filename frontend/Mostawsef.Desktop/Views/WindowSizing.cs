using System;
using Avalonia;

namespace Mostawsef.Desktop.Views;

public static class WindowSizing
{
    /// <summary>Share of the screen work area the restored (non-maximized) window may use.</summary>
    public const double MaxWorkAreaShare = 0.9;

    /// <summary>The preferred window size, shrunk to fit the screen work area (both in DIPs).</summary>
    public static Size FitToWorkArea(Size preferred, Size workArea) =>
        new(
            Math.Min(preferred.Width, Math.Floor(workArea.Width * MaxWorkAreaShare)),
            Math.Min(preferred.Height, Math.Floor(workArea.Height * MaxWorkAreaShare)));
}

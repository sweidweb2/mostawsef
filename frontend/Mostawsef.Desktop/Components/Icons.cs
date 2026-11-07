using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Mostawsef.Desktop.Components;

/// <summary>Access to icon geometries from Styles/Tokens/Icons.axaml in code.</summary>
public static class Icons
{
    public static Geometry? Get(string key) =>
        Application.Current?.TryFindResource(key, out var value) == true ? value as Geometry : null;
}

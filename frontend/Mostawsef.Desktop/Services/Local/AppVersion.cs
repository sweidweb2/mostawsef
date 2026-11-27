using System;
using System.Reflection;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// Product semver stamped into the Desktop assembly (<c>Version</c> / InformationalVersion).
/// Same number as Setup and the API when published via <c>build-setup.ps1</c>.
/// </summary>
public static class AppVersion
{
    public static string Current { get; } = Read(typeof(App).Assembly);

    internal static string Read(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        if (version is null)
        {
            return "0.0.0";
        }

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    /// <summary>Compare two semver-like strings (major.minor.patch…). Positive if <paramref name="left"/> is newer.</summary>
    public static int Compare(string left, string right)
    {
        if (!TryParse(left, out var a))
        {
            a = new Version(0, 0, 0);
        }

        if (!TryParse(right, out var b))
        {
            b = new Version(0, 0, 0);
        }

        return a.CompareTo(b);
    }

    private static bool TryParse(string value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var core = value.Trim();
        var dash = core.IndexOf('-');
        if (dash >= 0)
        {
            core = core[..dash];
        }

        var plus = core.IndexOf('+');
        if (plus >= 0)
        {
            core = core[..plus];
        }

        if (!Version.TryParse(core, out var parsed))
        {
            return false;
        }

        // Normalize to three components so 1.2 compares as 1.2.0.
        version = new Version(
            Math.Max(parsed.Major, 0),
            Math.Max(parsed.Minor, 0),
            Math.Max(parsed.Build, 0));
        return true;
    }
}

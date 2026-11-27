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
}

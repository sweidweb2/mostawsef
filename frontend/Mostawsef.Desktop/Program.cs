using Avalonia;
using Avalonia.Media;
using System;

namespace Mostawsef.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();

        // Developer Tools walk the visual tree on every attach/detach. That showed up as multi-second
        // Nav stalls + GC storms in Debug, so it is opt-in: set MOSTAWSEF_DEVTOOLS=1.
#if DEBUG
        if (string.Equals(Environment.GetEnvironmentVariable("MOSTAWSEF_DEVTOOLS"), "1", StringComparison.Ordinal))
        {
            builder = builder.WithDeveloperTools();
        }
#endif

        return builder
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://Mostawsef.Desktop/Assets/Fonts#IBM Plex Sans Arabic",
            })
            .LogToTrace();
    }
}

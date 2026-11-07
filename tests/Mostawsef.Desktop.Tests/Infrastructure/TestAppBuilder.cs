using Avalonia;
using Avalonia.Headless;
using Mostawsef.Desktop;
using Mostawsef.Desktop.Tests.Infrastructure;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// LocalizationService is a process-wide singleton, so desktop tests run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>Runs the real App (styles, themes, components) on Avalonia's headless platform: no window on screen.</summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

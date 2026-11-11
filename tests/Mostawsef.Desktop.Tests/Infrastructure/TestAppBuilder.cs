using Avalonia;
using Avalonia.Headless;
using Mostawsef.Desktop;
using Mostawsef.Desktop.Tests.Infrastructure;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

// Avalonia headless + App.Services are process-wide. A UI collection alone is not enough: other
// collections would still run beside it and crash the test host. Keep the whole assembly serial.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>Runs the real App (styles, themes, components) on Avalonia's headless platform: no window on screen.</summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

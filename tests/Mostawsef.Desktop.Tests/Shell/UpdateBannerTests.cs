using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Updates;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Tests.Shell;

[Collection(UiTestCollection.Name)]
public class UpdateBannerTests : UiTestBase
{
    public UpdateBannerTests()
    {
        UpdateChecker.MinCheckInterval = TimeSpan.Zero;
    }

    [AvaloniaFact]
    public async Task Portal_shows_the_update_banner_when_a_newer_setup_is_published()
    {
        using var app = new TestServices();
        app.Api.OnGet("/api/health", new HealthDto("Healthy", "Connected", ApiVersion.Current));
        app.Updates.Error = null;
        app.Updates.RespondWith(new UpdateFeed(
            "99.0.0",
            "https://example.com/Mostawsef-Setup.exe",
            "abc",
            "Release notes",
            null));

        var window = await ShowPortalAsync(app);
        Dispatcher.UIThread.RunJobs();

        var banner = window.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "UpdateBanner");
        var alert = window.GetVisualDescendants().OfType<Alert>().Single(a => a.Name == "UpdateAvailableAlert");
        Assert.True(banner.IsVisible);
        Assert.Contains("99.0.0", alert.Text);
        Assert.Contains("Download update", Texts(window));
    }

    [AvaloniaFact]
    public async Task Download_update_from_the_portal_opens_the_setup_url()
    {
        using var app = new TestServices();
        app.Api.OnGet("/api/health", new HealthDto("Healthy", "Connected", ApiVersion.Current));
        app.Updates.Error = null;
        const string url = "https://example.com/Mostawsef-Setup-9.9.9.exe";
        app.Updates.RespondWith(new UpdateFeed(
            "99.0.0",
            url,
            "abc",
            null,
            null));

        var window = await ShowPortalAsync(app);
        Dispatcher.UIThread.RunJobs();

        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "DownloadUpdateButton");
        button.Command!.Execute(null);

        Assert.Equal([url], app.OpenedUrls);
    }

    private static async Task<Window> ShowPortalAsync(TestServices app)
    {
        app.SignInAs(Roles.Admin);
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        // Start already ran CheckNowAsync; wait for the feed handler.
        await UiWait.UntilAsync(() => shell.Updates.CheckAttempts > 0 || shell.Updates.IsAvailable);
        await shell.Updates.CheckNowAsync(force: true);
        var window = app.Show(shell);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<string> Texts(Window window) => TestServices.Texts(window);
}

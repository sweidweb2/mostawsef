using System.Text;
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
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("setup"), notesEn: "Release notes");

        var (window, _) = await ShowPortalAsync(app);
        Dispatcher.UIThread.RunJobs();

        var banner = window.GetVisualDescendants().OfType<StackPanel>().Single(g => g.Name == "UpdateBanner");
        var alert = window.GetVisualDescendants().OfType<Alert>().Single(a => a.Name == "UpdateAvailableAlert");
        Assert.True(banner.IsVisible);
        Assert.Contains("99.0.0", alert.Text);
        Assert.Contains("Update now", TestServices.Texts(window));
    }

    [AvaloniaFact]
    public async Task Update_now_from_the_portal_starts_the_applicator_and_exits()
    {
        using var app = new TestServices();
        app.Api.OnGet("/api/health", new HealthDto("Healthy", "Connected", ApiVersion.Current));
        app.Updates.Error = null;
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("setup-bytes"));

        var (window, shell) = await ShowPortalAsync(app);
        Dispatcher.UIThread.RunJobs();

        await shell.Updates.InstallCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.UpdateHost.ApplyCalls);
        Assert.Equal(1, app.UpdateHost.ExitCalls);
        _ = window;
    }

    [AvaloniaFact]
    public async Task Super_portal_also_shows_the_update_banner()
    {
        using var app = new TestServices();
        app.Api.OnGet("/api/health", new HealthDto("Healthy", "Connected", ApiVersion.Current));
        app.Updates.Error = null;
        app.Updates.RespondWithSetup("99.0.0", Encoding.UTF8.GetBytes("setup"));

        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        await shell.Updates.CheckNowAsync(force: true);
        var window = app.Show(shell);
        Dispatcher.UIThread.RunJobs();

        Assert.True(shell.Updates.IsAvailable);
        Assert.Contains("Update now", TestServices.Texts(window));
    }

    private static async Task<(Window Window, PortalShellViewModel Shell)> ShowPortalAsync(TestServices app)
    {
        app.SignInAs(Roles.Admin);
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        await UiWait.UntilAsync(() => shell.Updates.CheckAttempts > 0 || shell.Updates.IsAvailable);
        await shell.Updates.CheckNowAsync(force: true);
        var window = app.Show(shell);
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }
}

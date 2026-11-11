using System.Net;
using System.Net.Http;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Shell;

[Collection(UiTestCollection.Name)]
public class ConnectionBannerTests : UiTestBase
{
    private const string HealthPath = "/api/health";

    [AvaloniaFact]
    public void No_banner_while_the_server_answers()
    {
        using var app = new TestServices();
        app.Api.OnGet(HealthPath, new HealthDto("Healthy", "Connected", ApiVersion.Current));

        var window = ShowPortal(app);

        Assert.False(Banner(window).IsVisible);
        Assert.Equal(ConnectionState.Online, app.Get<ConnectionMonitor>().State);
    }

    [AvaloniaFact]
    public async Task The_banner_appears_when_the_server_is_unreachable_and_clears_when_it_is_back()
    {
        using var app = new TestServices();
        app.Api.FailWithNetworkError();
        var window = ShowPortal(app);
        var banner = Banner(window);

        Assert.True(banner.IsVisible);
        Assert.Equal("Connection to the server was lost. Retrying...", banner.Text);
        Assert.Equal(BadgeKind.Warning, banner.Kind);

        app.Api.OnGet(HealthPath, new HealthDto("Healthy", "Connected", ApiVersion.Current));
        await app.Get<ConnectionMonitor>().CheckNowAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.False(banner.IsVisible);
    }

    [AvaloniaFact]
    public async Task Any_api_call_that_cannot_reach_the_server_shows_the_banner_at_once()
    {
        using var app = new TestServices();
        app.Api.OnGet(HealthPath, new HealthDto("Healthy", "Connected", ApiVersion.Current));
        var window = ShowPortal(app);

        app.Api.On(HttpMethod.Get, "/api/users", _ => throw new HttpRequestException("Connection refused"));
        await app.Get<UsersApi>().GetUsersAsync(1, 20, null, cancellationToken: TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Banner(window).IsVisible);
    }

    [AvaloniaFact]
    public void A_server_of_another_version_shows_the_update_warning()
    {
        using var app = new TestServices();
        app.Api.OnGet(HealthPath, new HealthDto("Healthy", "Connected", ApiVersion.Current + 1));

        var banner = Banner(ShowPortal(app));

        Assert.True(banner.IsVisible);
        Assert.Equal(BadgeKind.Danger, banner.Kind);
        Assert.StartsWith("This app does not match the server version", banner.Text);
    }

    [AvaloniaFact]
    public void Signing_out_stops_the_checks()
    {
        using var app = new TestServices();
        app.Api.FailWithNetworkError();
        ShowPortal(app);
        var monitor = app.Get<ConnectionMonitor>();
        Assert.True(monitor.IsRunning);

        app.Session.SignOut();

        Assert.False(monitor.IsRunning);
        Assert.Equal(ConnectionState.Online, monitor.State);
    }

    [AvaloniaFact]
    public async Task Health_checks_do_not_send_the_bearer_token()
    {
        using var app = new TestServices();
        app.Api.OnGet(HealthPath, new HealthDto("Healthy", "Connected", ApiVersion.Current));
        ShowPortal(app);

        await app.Get<ConnectionMonitor>().CheckNowAsync();

        var health = app.Api.RequestsTo(HttpMethod.Get, HealthPath).ToList();
        Assert.NotEmpty(health);
        Assert.All(health, r => Assert.Null(r.Headers.Authorization));
    }

    [AvaloniaFact]
    public async Task A_bad_health_payload_keeps_polling()
    {
        using var app = new TestServices();
        Func<HttpResponseMessage> health = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json"),
        };
        app.Api.On(HttpMethod.Get, HealthPath, _ => health());
        ShowPortal(app);
        var monitor = app.Get<ConnectionMonitor>();

        await monitor.CheckNowAsync();

        Assert.Equal(ConnectionState.Offline, monitor.State);
        Assert.True(monitor.IsRunning);

        health = () => FakeApiHandler.Json(new HealthDto("Healthy", "Connected", ApiVersion.Current));
        await monitor.CheckNowAsync();

        Assert.Equal(ConnectionState.Online, monitor.State);
    }

    private static Window ShowPortal(TestServices app)
    {
        app.SignInAs(Roles.Admin);
        app.Navigation.NavigateToHome();
        var window = app.Show(app.Navigation.CurrentPage!);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Alert Banner(Window window) =>
        window.GetVisualDescendants().OfType<Alert>().Single(a => a.Name == "ConnectionBanner");
}

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.ViewModels.Super;
using Mostawsef.Desktop.Views.Super;

namespace Mostawsef.Desktop.Tests.Shell;

public class PageLifecycleTests
{
    private const string ActivityPath = "/api/activity-logs";
    private const int Dashboard = 0;
    private const int ActivityLogs = 2;

    private static readonly ActivityLogListItemDto SignIn =
        new(Guid.NewGuid(), DateTime.UtcNow, "auth.signed_in", "Auth", "Signed in", "admin@test.local");

    [AvaloniaFact]
    public void Returning_to_a_page_reuses_it_without_reloading()
    {
        using var app = new TestServices();
        _ = new SlowServer(app) { Answers = true };
        var shell = OpenSuperPortal(app);

        shell.SelectedMenuIndex = ActivityLogs;
        var page = shell.CurrentPage;
        var requests = app.Api.Requests.Count;

        shell.SelectedMenuIndex = Dashboard;
        shell.SelectedMenuIndex = ActivityLogs;

        Assert.Same(page, shell.CurrentPage);
        Assert.Equal(requests, app.Api.Requests.Count);
    }

    [AvaloniaFact]
    public void Returning_to_a_page_builds_a_new_view()
    {
        using var app = new TestServices();
        _ = new SlowServer(app) { Answers = true };
        var shell = OpenSuperPortal(app);
        var window = app.Show(shell);

        shell.SelectedMenuIndex = ActivityLogs;
        Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<ActivityLogsView>().Single();

        shell.SelectedMenuIndex = Dashboard;
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(window.GetVisualDescendants().OfType<ActivityLogsView>());

        shell.SelectedMenuIndex = ActivityLogs;
        Dispatcher.UIThread.RunJobs();
        Assert.NotSame(view, window.GetVisualDescendants().OfType<ActivityLogsView>().Single());
    }

    [AvaloniaFact]
    public async Task Leaving_a_page_cancels_its_requests_and_coming_back_reloads_the_same_page()
    {
        using var app = new TestServices();
        var server = new SlowServer(app);
        var shell = OpenSuperPortal(app);

        shell.SelectedMenuIndex = ActivityLogs;
        var page = Assert.IsType<ActivityLogsViewModel>(shell.CurrentPage);
        Assert.True(page.IsLoading);
        Assert.NotEmpty(server.Waiting);

        shell.SelectedMenuIndex = Dashboard;
        await WaitUntil(() => !page.IsLoading);

        Assert.All(server.Waiting, request => Assert.True(request.IsCancellationRequested));
        Assert.False(page.HasLoadError);

        server.Answers = true;
        server.Waiting.Clear();
        shell.SelectedMenuIndex = ActivityLogs;
        var next = Assert.IsType<ActivityLogsViewModel>(shell.CurrentPage);
        await WaitUntil(() => !next.IsLoading);

        Assert.Same(page, next);
        Assert.Single(next.Items);
        Assert.Equal(1, next.Stats!.Total);
    }

    [AvaloniaFact]
    public async Task Signing_out_cancels_the_open_page_requests()
    {
        using var app = new TestServices();
        var server = new SlowServer(app);
        var shell = OpenSuperPortal(app);
        shell.SelectedMenuIndex = ActivityLogs;
        var page = Assert.IsType<ActivityLogsViewModel>(shell.CurrentPage);

        app.Session.SignOut();
        await WaitUntil(() => !page.IsLoading);

        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
        Assert.NotEmpty(server.Waiting);
        Assert.All(server.Waiting, request => Assert.True(request.IsCancellationRequested));
    }

    [AvaloniaFact]
    public async Task Details_that_arrive_after_leaving_the_page_do_not_open_a_modal()
    {
        using var app = new TestServices();
        var server = new SlowServer(app) { Answers = true };
        var shell = OpenSuperPortal(app);
        var host = new DialogHost { Content = new ContentControl { Content = shell } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        shell.SelectedMenuIndex = ActivityLogs;
        var page = Assert.IsType<ActivityLogsViewModel>(shell.CurrentPage);

        server.Answers = false;
        var view = page.ViewDetailsCommand.ExecuteAsync(SignIn);
        var details = Assert.Single(server.Waiting);

        shell.SelectedMenuIndex = Dashboard;
        await view;
        Dispatcher.UIThread.RunJobs();

        Assert.True(details.IsCancellationRequested);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
    }

    private static PortalShellViewModel OpenSuperPortal(TestServices app)
    {
        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();
        return Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    /// <summary>
    /// The activity log endpoints: they answer at once when Answers is true, otherwise they keep the request open
    /// until the app cancels it. Waiting holds the cancellation token of every request left open.
    /// </summary>
    private sealed class SlowServer
    {
        public SlowServer(TestServices app) =>
            app.Api.On(HttpMethod.Get, ActivityPath, (request, cancellationToken) =>
            {
                if (Answers)
                {
                    return Task.FromResult(Answer(request.RequestUri!.AbsolutePath));
                }

                Waiting.Add(cancellationToken);
                var response = new TaskCompletionSource<HttpResponseMessage>();
                cancellationToken.Register(() => response.TrySetCanceled(cancellationToken));
                return response.Task;
            });

        public bool Answers { get; set; }

        public List<CancellationToken> Waiting { get; } = [];

        private static HttpResponseMessage Answer(string path) =>
            path.EndsWith("/stats", StringComparison.Ordinal) ? FakeApiHandler.Json(new ActivityLogStatsDto(1, 1, 1, 0))
            : path == ActivityPath ? FakeApiHandler.Json(new CursorPage<ActivityLogListItemDto>([SignIn], 20, 1, null, null))
            : FakeApiHandler.Json(new ActivityLogDto(
                SignIn.Id, SignIn.OccurredAt, SignIn.Action, SignIn.Module, SignIn.Description, null, SignIn.Username,
                null, null, null, null, null));
    }
}

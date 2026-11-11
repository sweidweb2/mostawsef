using System.Net;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.ViewModels.Super;

namespace Mostawsef.Desktop.Tests.Logs;

[Collection(UiTestCollection.Name)]
public class LogPagesTests : UiTestBase
{
    private const string ActivityPath = "/api/activity-logs";
    private const string ErrorsPath = "/api/system-errors";

    private static readonly ActivityLogListItemDto SignIn =
        new(Guid.NewGuid(), DateTime.UtcNow, "auth.signed_in", "Auth", "Signed in", "admin@test.local");

    [AvaloniaFact]
    public void Super_portal_menu_has_dashboard_users_activity_logs_and_system_errors()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();

        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);

        Assert.Equal(
            ["Nav.Dashboard", "Nav.Users", "Nav.ActivityLogs", "Nav.SystemErrors", "Nav.SystemSettings"],
            shell.MenuItems.Select(i => i.TitleKey));
    }

    [AvaloniaFact]
    public void Opening_activity_logs_from_the_menu_loads_stats_and_the_first_page()
    {
        using var app = SuperWithActivity(total: 45, SignIn);
        var shell = OpenSuperPortal(app);

        shell.SelectedMenuIndex = 2;

        var page = Assert.IsType<ActivityLogsViewModel>(shell.CurrentPage);
        Assert.Equal(45, page.Stats!.Total);
        Assert.Single(page.Items);
        Assert.Equal(45, page.TotalCount);
        Assert.Equal(3, page.PageCount);
        Assert.Equal("?pageSize=20&includeTotal=false", Assert.Single(ListRequests(app, ActivityPath)));
    }

    [AvaloniaFact]
    public async Task Next_page_follows_the_cursor_without_counting_and_search_goes_back_to_page_one()
    {
        using var app = SuperWithActivity(total: 45, SignIn);
        var page = app.Get<ActivityLogsViewModel>();
        await page.ActivateAsync();

        page.CurrentPage = 2;
        await UiWait.UntilAsync(() => !page.IsLoading);
        Assert.Equal("?pageSize=20&after=older-cursor&includeTotal=false", LastListRequest(app, ActivityPath));
        Assert.Equal(2, page.CurrentPage);
        Assert.Equal(45, page.TotalCount);
        Assert.Equal(3, page.PageCount);
        Assert.Equal(21, page.FirstRowNumber);

        page.Search = "admin";
        await page.SearchCommand.ExecuteAsync(null);
        Assert.Equal(1, page.CurrentPage);
        Assert.Equal(1, page.FirstRowNumber);
        Assert.Equal("?pageSize=20&search=admin", LastListRequest(app, ActivityPath));
    }

    [AvaloniaFact]
    public async Task Rows_are_numbered_and_the_numbering_continues_on_the_next_page()
    {
        using var app = SuperWithActivity(total: 45, SignIn);
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);

        Assert.Equal("1", window.GetVisualDescendants().OfType<RowNumber>().Single().Text);

        page.CurrentPage = 2;
        await UiWait.UntilAsync(() => !page.IsLoading);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("21", window.GetVisualDescendants().OfType<RowNumber>().Single().Text);
    }

    [AvaloniaFact]
    public async Task Previous_page_follows_the_cursor_of_the_shown_page()
    {
        using var app = SuperWithActivity(total: 45, SignIn);
        var page = app.Get<ActivityLogsViewModel>();
        await page.ActivateAsync();
        page.CurrentPage = 3;
        await UiWait.UntilAsync(() => !page.IsLoading);

        page.CurrentPage = 2;
        await UiWait.UntilAsync(() => !page.IsLoading);

        Assert.Equal("?pageSize=20&before=newer-cursor&includeTotal=false", LastListRequest(app, ActivityPath));
        Assert.Equal(2, page.CurrentPage);
    }

    [AvaloniaFact]
    public async Task Last_page_asks_for_the_oldest_rows_that_fill_the_last_page()
    {
        using var app = SuperWithActivity(total: 45, SignIn);
        var page = app.Get<ActivityLogsViewModel>();
        await page.ActivateAsync();

        page.CurrentPage = 3;
        await UiWait.UntilAsync(() => !page.IsLoading);

        Assert.Equal("?pageSize=5&last=true&includeTotal=false", LastListRequest(app, ActivityPath));
        Assert.Equal(3, page.CurrentPage);
        Assert.Equal(3, page.PageCount);
    }

    [AvaloniaFact]
    public async Task Paging_and_searching_reuse_the_stats_and_only_refresh_reloads_them()
    {
        using var app = SuperWithActivity(total: 45, SignIn);
        var page = app.Get<ActivityLogsViewModel>();
        await page.ActivateAsync();

        page.CurrentPage = 2;
        await UiWait.UntilAsync(() => !page.IsLoading);
        page.Search = "admin";
        await page.SearchCommand.ExecuteAsync(null);
        Assert.Equal(1, StatsRequests(app, ActivityPath));

        await page.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, StatsRequests(app, ActivityPath));
        Assert.Equal("?pageSize=20&search=admin", LastListRequest(app, ActivityPath));
    }

    [AvaloniaFact]
    public async Task Pagination_is_disabled_while_a_page_loads()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        var slow = HoldPageTwo(app, _ => SignIn);
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);
        var pagination = window.GetVisualDescendants().OfType<Pagination>().Single();
        Assert.True(pagination.IsEnabled);

        page.CurrentPage = 2;
        Dispatcher.UIThread.RunJobs();
        Assert.False(pagination.IsEnabled);

        slow.Release.SetResult();
        await UiWait.UntilAsync(() => !page.IsLoading);
        Assert.True(pagination.IsEnabled);
    }

    [AvaloniaFact]
    public async Task Deleting_the_only_row_of_the_last_page_shows_the_new_last_page()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        var deleted = false;
        app.Api
            .On(HttpMethod.Get, $"{ActivityPath}/stats", _ => FakeApiHandler.Json(new ActivityLogStatsDto(deleted ? 20 : 21, 0, 0, 0)))
            .On(HttpMethod.Delete, ActivityPath, _ =>
            {
                deleted = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            })
            .On(HttpMethod.Get, ActivityPath, request =>
                deleted && request.RequestUri!.Query.Contains("after=")
                    ? ListAnswer(request, 20, [])
                    : ListAnswer(request, deleted ? 20 : 21, [SignIn]));
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);
        page.CurrentPage = 2;
        await UiWait.UntilAsync(() => !page.IsLoading);

        var delete = page.DeleteCommand.ExecuteAsync(SignIn);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;

        Assert.Equal("?pageSize=20&last=true&includeTotal=false", LastListRequest(app, ActivityPath));
        Assert.Equal(1, page.CurrentPage);
        Assert.Equal(1, page.PageCount);
        Assert.Equal(20, page.TotalCount);
    }

    [AvaloniaFact]
    public async Task A_slow_page_that_answers_late_does_not_replace_the_page_requested_after_it()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        var pageThree = SignIn with { Id = Guid.NewGuid(), Description = "Page three" };
        var slow = HoldPageTwo(app, query => query.Contains("last=true") ? pageThree : SignIn);
        var page = app.Get<ActivityLogsViewModel>();
        await page.ActivateAsync();

        page.CurrentPage = 2;
        page.CurrentPage = 3;
        await UiWait.UntilAsync(() => page.Items.Contains(pageThree) && !page.IsLoading);
        slow.Release.SetResult();
        await UiWait.DrainAsync();

        Assert.True(slow.RequestToken.IsCancellationRequested);
        Assert.Equal(3, page.CurrentPage);
        Assert.Equal(pageThree, Assert.Single(page.Items));
        Assert.False(page.IsLoading);
    }

    [AvaloniaFact]
    public async Task Searching_while_a_page_is_loading_cancels_it_and_shows_the_search_results()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        var match = SignIn with { Id = Guid.NewGuid(), Description = "Matches admin" };
        var slow = HoldPageTwo(app, query => query.Contains("search=admin") ? match : SignIn);
        var page = app.Get<ActivityLogsViewModel>();
        await page.ActivateAsync();

        page.CurrentPage = 2;
        page.Search = "admin";
        await page.SearchCommand.ExecuteAsync(null);
        slow.Release.SetResult();
        await UiWait.DrainAsync();

        Assert.True(slow.RequestToken.IsCancellationRequested);
        Assert.Equal(1, page.CurrentPage);
        Assert.Equal(match, Assert.Single(page.Items));
        Assert.False(page.IsLoading);
    }

    [AvaloniaFact]
    public async Task Delete_asks_to_confirm_then_deletes_and_reloads()
    {
        using var app = SuperWithActivity(total: 1, SignIn);
        app.Api.On(HttpMethod.Delete, ActivityPath, HttpStatusCode.NoContent);
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);
        var listRequestsBefore = app.Api.RequestsTo(HttpMethod.Get, ActivityPath).Count();

        var delete = page.DeleteCommand.ExecuteAsync(SignIn);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Delete, ActivityPath));

        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;

        var deleteRequest = Assert.Single(app.Api.RequestsTo(HttpMethod.Delete, ActivityPath));
        Assert.EndsWith(SignIn.Id.ToString(), deleteRequest.RequestUri!.AbsolutePath);
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, ActivityPath).Count() > listRequestsBefore);
        Assert.Equal(2, StatsRequests(app, ActivityPath));
        Assert.Contains("includeTotal=false", LastListRequest(app, ActivityPath), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Cancelling_delete_does_not_call_the_api()
    {
        using var app = SuperWithActivity(total: 1, SignIn);
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);

        var delete = page.DeleteCommand.ExecuteAsync(SignIn);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(false);
        await delete;

        Assert.Empty(app.Api.RequestsTo(HttpMethod.Delete, ActivityPath));
    }

    [AvaloniaFact]
    public async Task The_eye_opens_a_modal_with_every_detail_of_the_activity()
    {
        using var app = SuperWithActivity(total: 1, SignIn);
        app.Api.OnGet($"{ActivityPath}/{SignIn.Id}", new ActivityLogDto(
            SignIn.Id, SignIn.OccurredAt, "auth.signed_in", "Auth", "Signed in", Guid.NewGuid(), "admin@test.local",
            null, null, "10.0.0.7", "Mostawsef.Desktop/1.0", """{"role":"Admin"}"""));
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);

        var view = page.ViewDetailsCommand.ExecuteAsync(SignIn);
        Dispatcher.UIThread.RunJobs();

        var modal = window.GetVisualDescendants().OfType<Modal>().Single();
        Assert.Equal("Activity details", modal.Title);
        var values = window.GetVisualDescendants().OfType<DetailsItem>().ToDictionary(i => i.Label!, i => i.DisplayValue);
        Assert.Equal("Signed in", values["Action"]);
        Assert.Equal("auth.signed_in", values["Action code"]);
        Assert.Equal("10.0.0.7", values["IP address"]);
        Assert.Contains("\"role\": \"Admin\"", values["Details"]);
        Assert.Equal("—", values["Record"]);

        modal.Close();
        await view;
    }

    [AvaloniaFact]
    public async Task Long_descriptions_are_cut_in_the_table_with_the_full_text_in_a_tooltip()
    {
        var longText = "Imported " + new string('x', 300);
        var item = SignIn with { Description = longText };
        using var app = SuperWithActivity(total: 1, item);
        var (_, window) = await ShowPage<ActivityLogsViewModel>(app);

        var cell = window.GetVisualDescendants().OfType<TextBlock>().Single(t => Equals(ToolTip.GetTip(t), longText));

        Assert.EndsWith("…", cell.Text);
        Assert.True(cell.Text!.Length <= 71, $"Cell shows {cell.Text.Length} characters");
    }

    [AvaloniaFact]
    public async Task System_errors_page_shows_stats_translated_levels_and_the_stack_trace_in_details()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        var error = new SystemErrorListItemDto(Guid.NewGuid(), DateTime.UtcNow, "Critical", "Desktop", "Crash on save", null, null, "super@test.local");
        app.Api
            .OnGet($"{ErrorsPath}/stats", new SystemErrorStatsDto(7, 2, 5, 1))
            .OnGet(ErrorsPath, new CursorPage<SystemErrorListItemDto>([error], 20, 1, null, null))
            .OnGet($"{ErrorsPath}/{error.Id}", new SystemErrorDto(
                error.Id, error.OccurredAt, "Critical", "Desktop", "Crash on save", "System.NullReferenceException",
                "at Mostawsef.Desktop.Save()", null, null, null, null, "super@test.local", null, null, null));
        var (page, window) = await ShowPage<SystemErrorsViewModel>(app);

        Assert.Equal(7, page.Stats!.Total);
        Assert.Equal(1, page.Stats.Critical);
        Assert.Contains(window.GetVisualDescendants().OfType<Badge>(), b => b.Text == "Critical" && b.Kind == BadgeKind.Danger);

        var view = page.ViewDetailsCommand.ExecuteAsync(error);
        Dispatcher.UIThread.RunJobs();
        var stack = window.GetVisualDescendants().OfType<DetailsItem>().Single(i => i.Label == "Stack trace");
        Assert.True(stack.IsCode);
        Assert.Equal("at Mostawsef.Desktop.Save()", stack.DisplayValue);

        window.GetVisualDescendants().OfType<Modal>().Single().Close();
        await view;
    }

    [AvaloniaFact]
    public async Task A_failed_load_keeps_the_page_usable_and_reports_an_error_instead_of_empty()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.FailWithNetworkError();
        var page = app.Get<ActivityLogsViewModel>();

        await page.ActivateAsync();

        Assert.False(page.IsLoading);
        Assert.Empty(page.Items);
        Assert.True(page.HasLoadError);
        Assert.False(page.IsEmpty);
        Assert.Null(page.Stats);
    }

    [AvaloniaFact]
    public async Task Unreachable_server_shows_a_connection_error_with_retry_in_the_table()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.FailWithNetworkError();
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);

        var error = window.GetVisualDescendants().OfType<ErrorState>().Single();
        Assert.True(error.IsEffectivelyVisible);
        Assert.Equal("Cannot reach the server", error.Title);
        Assert.Equal("Make sure the Mostawsef service is running, then try again.", error.Description);
        Assert.Same(page.RefreshCommand, error.RetryCommand);
        Assert.Contains("Try again", TestServices.Texts(window));
        Assert.False(window.GetVisualDescendants().OfType<EmptyState>().Single().IsEffectivelyVisible);

        app.Localization.SetLanguage(AppLanguage.Arabic);

        Assert.Equal("تعذّر الاتصال بالخادم", error.Title);
    }

    [AvaloniaFact]
    public async Task A_server_error_shows_the_generic_load_error()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.RespondWithStatus(HttpStatusCode.InternalServerError);
        var (_, window) = await ShowPage<SystemErrorsViewModel>(app);

        var error = window.GetVisualDescendants().OfType<ErrorState>().Single();
        Assert.True(error.IsEffectivelyVisible);
        Assert.Equal("Something went wrong", error.Title);
        Assert.Equal("We couldn't load this data. Please try again.", error.Description);
    }

    [AvaloniaFact]
    public async Task Retry_loads_the_rows_and_hides_the_error_once_the_server_answers()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.FailWithNetworkError();
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);
        var error = window.GetVisualDescendants().OfType<ErrorState>().Single();

        app.Api
            .OnGet($"{ActivityPath}/stats", new ActivityLogStatsDto(1, 1, 1, 0))
            .OnGet(ActivityPath, new CursorPage<ActivityLogListItemDto>([SignIn], 20, 1, null, null));
        await page.RefreshCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(page.HasLoadError);
        Assert.Single(page.Items);
        Assert.False(error.IsEffectivelyVisible);
    }

    private static TestServices SuperWithActivity(int total, params ActivityLogListItemDto[] items)
    {
        var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api
            .OnGet($"{ActivityPath}/stats", new ActivityLogStatsDto(total, 3, 10, 1))
            .On(HttpMethod.Get, ActivityPath, request => ListAnswer(request, total, items));
        return app;
    }

    /// <summary>
    /// Answers a list request like the server: the first page has no previous cursor, the last no next cursor,
    /// and the total is left out when the request says includeTotal=false.
    /// </summary>
    private static HttpResponseMessage ListAnswer(HttpRequestMessage request, int total, IReadOnlyList<ActivityLogListItemDto> items)
    {
        var query = request.RequestUri!.Query;
        (string? Next, string? Previous) cursors =
            query.Contains("last=true") ? (null, "newer-cursor")
            : query.Contains("after=") || query.Contains("before=") ? ("older-cursor", "newer-cursor")
            : (total > 20 ? "older-cursor" : null, null);
        return FakeApiHandler.Json(new CursorPage<ActivityLogListItemDto>(
            items, 20, query.Contains("includeTotal=false") ? null : total, cursors.Next, cursors.Previous));
    }

    private static IEnumerable<string> ListRequests(TestServices app, string path) =>
        app.Api.RequestsTo(HttpMethod.Get, path).Where(r => !r.RequestUri!.AbsolutePath.EndsWith("/stats", StringComparison.Ordinal)).Select(r => r.RequestUri!.Query);

    private static int StatsRequests(TestServices app, string path) => app.Api.RequestsTo(HttpMethod.Get, $"{path}/stats").Count();

    private static PortalShellViewModel OpenSuperPortal(TestServices app)
    {
        app.Navigation.NavigateToHome();
        return Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
    }

    /// <summary>Shows the page inside a DialogHost (attached to the DialogService) so confirms and modals can open.</summary>
    private static async Task<(T Page, Window Window)> ShowPage<T>(TestServices app) where T : ILoadablePage
    {
        var page = app.Get<T>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }

    /// <summary>
    /// Page 2 of the activity list (the first "after" cursor) answers only when Release is set, and ignores cancellation
    /// like a slow server; other pages answer at once. 60 rows, so the last page is page 3.
    /// </summary>
    private static SlowResponse HoldPageTwo(TestServices app, Func<string, ActivityLogListItemDto> itemForQuery)
    {
        var slow = new SlowResponse();
        app.Api.OnGet($"{ActivityPath}/stats", new ActivityLogStatsDto(60, 0, 0, 0));
        app.Api.On(HttpMethod.Get, ActivityPath, async (request, cancellationToken) =>
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("after="))
            {
                slow.RequestToken = cancellationToken;
                await slow.Release.Task;
                return ListAnswer(request, 60, [SignIn with { Description = "Page two" }]);
            }

            return ListAnswer(request, 60, [itemForQuery(query)]);
        });
        return slow;
    }

    private sealed class SlowResponse
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken RequestToken { get; set; }
    }

    private static string LastListRequest(TestServices app, string path) =>
        app.Api.RequestsTo(HttpMethod.Get, path).Last(r => !r.RequestUri!.AbsolutePath.EndsWith("/stats", StringComparison.Ordinal)).RequestUri!.Query;
}

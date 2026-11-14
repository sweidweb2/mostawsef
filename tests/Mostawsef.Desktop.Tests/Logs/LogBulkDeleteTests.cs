using System.Net;
using System.Net.Http.Json;
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
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.ViewModels.Super;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Logs;

[Collection(UiTestCollection.Name)]
public class LogBulkDeleteTests : UiTestBase
{
    private const string ActivityPath = "/api/activity-logs";
    private const string ErrorsPath = "/api/system-errors";

    private static readonly ActivityLogListItemDto First =
        new(Guid.NewGuid(), DateTime.UtcNow, "auth.signed_in", "Auth", "Signed in", "admin@test.local");

    private static readonly ActivityLogListItemDto Second =
        new(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-1), "auth.signed_out", "Auth", "Signed out", "admin@test.local");

    [AvaloniaFact]
    public async Task Select_mode_shows_the_checkbox_column_and_leaving_it_clears_the_selection()
    {
        using var app = SuperWithActivity(First, Second);
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);
        var grid = window.GetVisualDescendants().OfType<DataGrid>().Single();

        Assert.False(grid.Columns[0].IsVisible);
        Assert.False(Named<Border>(window, "SelectionBar").IsVisible);

        page.ToggleSelectModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(grid.Columns[0].IsVisible);
        Assert.True(Named<Border>(window, "SelectionBar").IsVisible);

        page.ToggleRowCommand.Execute(First);
        Dispatcher.UIThread.RunJobs();
        Assert.True(page.Selection.Contains(First.Id));
        Assert.Contains("1 selected", TestServices.Texts(window));
        Assert.Single(window.GetVisualDescendants().OfType<CheckBox>(), c => c.Classes.Contains("row-select") && c.IsChecked == true);
        Assert.False(page.IsPageSelected);

        page.ToggleSelectModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(page.Selection.HasAny);
        Assert.False(grid.Columns[0].IsVisible);
    }

    [AvaloniaFact]
    public async Task Selecting_the_page_and_deleting_sends_the_ids_then_reloads()
    {
        using var app = SuperWithActivity(First, Second);
        DeleteLogsRequest? sent = null;
        app.Api.On(HttpMethod.Post, $"{ActivityPath}/bulk-delete", request =>
        {
            sent = request.Content!.ReadFromJsonAsync<DeleteLogsRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(new DeleteLogsResultDto(sent!.Ids.Count));
        });
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);
        var listRequests = app.Api.RequestsTo(HttpMethod.Get, ActivityPath).Count();

        page.ToggleSelectModeCommand.Execute(null);
        Assert.False(page.DeleteSelectedCommand.CanExecute(null));
        page.IsPageSelected = true;
        Assert.True(page.IsPageSelected);
        Assert.Equal(2, page.Selection.Count);
        Assert.True(page.DeleteSelectedCommand.CanExecute(null));

        var delete = page.DeleteSelectedCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("Delete the 2 selected records? This cannot be undone.", TestServices.Texts(window));
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new[] { First.Id, Second.Id }.Order(), sent!.Ids.Order());
        Assert.Contains("2 records deleted.", Toasts(window));
        Assert.False(page.Selection.IsActive);
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, ActivityPath).Count() > listRequests);
    }

    [AvaloniaFact]
    public async Task Delete_all_asks_first_then_empties_the_log()
    {
        using var app = SuperWithActivity(First);
        app.Api.On(HttpMethod.Delete, ActivityPath, _ => FakeApiHandler.Json(new DeleteLogsResultDto(1)));
        var (page, window) = await ShowPage<ActivityLogsViewModel>(app);

        var cancelled = page.DeleteAllCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("Delete ALL activity log records? This cannot be undone.", TestServices.Texts(window));
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(false);
        await cancelled;
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Delete, ActivityPath));

        var delete = page.DeleteAllCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.Api.RequestsTo(HttpMethod.Delete, ActivityPath));
        Assert.Contains("All records deleted.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task System_errors_page_deletes_the_selection_through_its_own_endpoint()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        var error = new SystemErrorListItemDto(Guid.NewGuid(), DateTime.UtcNow, "Error", "Api", "Boom", "/api/x", 500, null);
        app.Api
            .OnGet($"{ErrorsPath}/stats", new SystemErrorStatsDto(1, 1, 1, 0))
            .OnGet(ErrorsPath, new CursorPage<SystemErrorListItemDto>([error], 20, 1, null, null))
            .On(HttpMethod.Post, $"{ErrorsPath}/bulk-delete", _ => FakeApiHandler.Json(new DeleteLogsResultDto(1)));
        var (page, window) = await ShowPage<SystemErrorsViewModel>(app);

        page.ToggleSelectModeCommand.Execute(null);
        page.ToggleRowCommand.Execute(error);
        var delete = page.DeleteSelectedCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;

        Assert.Single(app.Api.RequestsTo(HttpMethod.Post, $"{ErrorsPath}/bulk-delete"));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, $"{ActivityPath}/bulk-delete"));
    }

    private static TestServices SuperWithActivity(params ActivityLogListItemDto[] items)
    {
        var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api
            .OnGet($"{ActivityPath}/stats", new ActivityLogStatsDto(items.Length, 0, 0, 0))
            .OnGet(ActivityPath, new CursorPage<ActivityLogListItemDto>(items, 20, items.Length, null, null));
        return app;
    }

    /// <summary>Shows the page inside a DialogHost and with toasts, so confirms, modals and toasts can open.</summary>
    private static async Task<(T Page, Window Window)> ShowPage<T>(TestServices app) where T : ILoadablePage
    {
        var page = app.Get<T>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        app.Get<ToastService>().Attach(window);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }
}

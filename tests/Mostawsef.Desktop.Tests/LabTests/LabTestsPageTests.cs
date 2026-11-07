using System.Net;
using System.Net.Http.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.LabTests;

public class LabTestsPageTests
{
    private const string TestsPath = "/api/lab-tests";

    private static readonly LabTestDto Cbc =
        new(Guid.NewGuid(), "CBC", "Complete blood count", 15050, 9000, true, DateTime.UtcNow, null, null, 2);

    private static readonly LabTestDto Sugar =
        new(Guid.NewGuid(), "FBS", "تحليل السكر الصائم", 4000, 2500, false, DateTime.UtcNow, null, null, 0);

    [AvaloniaFact]
    public void Only_admins_can_open_the_lab_tests_page()
    {
        using var app = new TestServices();
        Assert.False(app.Navigation.CanAccess(typeof(LabTestsViewModel)));

        app.SignInAs(Roles.Super);
        Assert.False(app.Navigation.CanAccess(typeof(LabTestsViewModel)));

        app.SignInAs(Roles.Admin);
        Assert.True(app.Navigation.CanAccess(typeof(LabTestsViewModel)));
    }

    [AvaloniaFact]
    public async Task The_table_lists_each_test_with_its_prices_status_and_row_actions()
    {
        using var app = AdminWithTests(Cbc, Sugar);

        var (page, window) = await ShowPage(app);

        Assert.False(page.IsGridView);
        Assert.True(Table(window).IsEffectivelyVisible);
        Assert.Empty(VisibleCards(window));
        var texts = Texts(window);
        Assert.Contains("Complete blood count", texts);
        Assert.Contains("CBC", texts);
        Assert.Contains("150.50", texts);
        Assert.Contains("90.00", texts);
        Assert.Contains("2 tests", texts);
        Assert.Contains("Unavailable", window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text));
        Assert.Equal(2, RowButtons(Table(window)).Count(b => b.Command == page.EditCommand));
        Assert.Equal(2, RowButtons(Table(window)).Count(b => b.Command == page.DeleteCommand));
    }

    [AvaloniaFact]
    public async Task The_grid_shows_a_square_card_per_test_with_edit_and_delete()
    {
        using var app = AdminWithTests(Cbc, Sugar);
        var (page, window) = await ShowPage(app);

        page.ShowGridCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Table(window).IsEffectivelyVisible);
        var cards = window.GetVisualDescendants().OfType<Card>().ToList();
        Assert.Equal<string?>([Cbc.Name, Sugar.Name], cards.Select(c => c.Title));
        Assert.All(cards, card =>
        {
            Assert.True(card.Bounds.Height >= card.Bounds.Width - 1, $"{card.Bounds.Width}x{card.Bounds.Height}");
            Assert.Equal<object?>([page.EditCommand, page.DeleteCommand], RowButtons(card).Select(b => b.Command));
        });
        Assert.Contains("selected", Named<Button>(window, "GridViewButton").Classes);
        Assert.DoesNotContain("selected", Named<Button>(window, "TableViewButton").Classes);

        page.ShowTableCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Table(window).IsEffectivelyVisible);
        Assert.Empty(VisibleCards(window));
    }

    [AvaloniaFact]
    public async Task Edit_on_a_card_opens_the_form_filled_in_and_saves_prices_in_minor_units()
    {
        using var app = AdminWithTests(Cbc);
        app.Api.OnGet($"{TestsPath}/{Cbc.Id}", Cbc);
        UpdateLabTestRequest? sent = null;
        app.Api.On(HttpMethod.Put, $"{TestsPath}/{Cbc.Id}", request =>
        {
            sent = request.Content!.ReadFromJsonAsync<UpdateLabTestRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(Cbc with { Price = sent!.Price!.Value, Version = 3 });
        });
        var (page, window) = await ShowPage(app);
        page.ShowGridCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var listRequests = app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count();

        var edit = RowButtons(window.GetVisualDescendants().OfType<Card>().Single()).First();
        edit.Command!.Execute(edit.CommandParameter);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm(window);
        Assert.False(form.IsNew);
        Assert.Equal(("CBC", "Complete blood count", 150.50m, 90.00m, true), (form.Code, form.Name, form.Price, form.LabCost, form.IsActive));
        Assert.Equal("Edit lab test", window.GetVisualDescendants().OfType<Modal>().Single().Title);

        form.Price = 200.25m;
        form.IsActive = false;
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            new UpdateLabTestRequest { Code = "CBC", Name = "Complete blood count", Price = 20025, LabCost = 9000, IsActive = false, Version = Cbc.Version },
            sent);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count() > listRequests + 1);
        Assert.Contains("Lab test saved.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Saving_an_empty_new_test_shows_field_errors_without_calling_the_api()
    {
        using var app = AdminWithTests(Cbc);
        var (page, window) = await ShowPage(app);
        var form = OpenAddForm(page, window);

        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Required, form.Errors[nameof(form.Code)]);
        Assert.Equal(ErrorCodes.Required, form.Errors[nameof(form.Name)]);
        Assert.Equal(ErrorCodes.Required, form.Errors[nameof(form.LabCost)]);
        Assert.Equal("This field is required.", FieldError(window, "Price"));
        Assert.False(Named<CheckBox>(window, "IsActiveBox").IsEffectivelyVisible);

        form.Price = -1;
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Must be 0 or more.", FieldError(window, "Price"));
        app.Localization.SetLanguage(AppLanguage.Arabic);
        Assert.Equal("يجب أن يكون 0 أو أكثر.", FieldError(window, "السعر"));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, TestsPath));
    }

    [AvaloniaFact]
    public async Task A_new_test_is_sent_trimmed_in_minor_units_then_the_list_reloads()
    {
        using var app = AdminWithTests(Cbc);
        CreateLabTestRequest? sent = null;
        app.Api.On(HttpMethod.Post, TestsPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<CreateLabTestRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(Sugar);
        });
        var (page, window) = await ShowPage(app);
        var listRequests = app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count();

        var add = page.AddCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm(window);
        form.Code = " fbs ";
        form.Name = "  Fasting blood sugar ";
        form.Price = 40;
        form.LabCost = 25.5m;
        await form.SaveCommand.ExecuteAsync(null);
        await add;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new CreateLabTestRequest { Code = "fbs", Name = "Fasting blood sugar", Price = 4000, LabCost = 2550 }, sent);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count() > listRequests);
        Assert.Contains("Lab test saved.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task A_taken_code_from_the_server_shows_next_to_the_code_field_and_keeps_the_form_open()
    {
        using var app = AdminWithTests(Cbc);
        app.Api.On(HttpMethod.Post, TestsPath, _ =>
            FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.Taken, ("Code", ErrorCodes.Taken)));
        var (page, window) = await ShowPage(app);
        var form = OpenAddForm(page, window);
        form.Code = "CBC";
        form.Name = "Blood count";
        form.Price = 10;
        form.LabCost = 5;

        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Another record already uses this value.", FieldError(window, "Code"));
        Assert.False(form.HasFormError);
        Assert.Single(window.GetVisualDescendants().OfType<Modal>());
    }

    [AvaloniaFact]
    public async Task Delete_asks_first_then_deletes_and_reloads()
    {
        using var app = AdminWithTests(Cbc);
        app.Api.On(HttpMethod.Delete, $"{TestsPath}/{Cbc.Id}", HttpStatusCode.NoContent);
        var (page, window) = await ShowPage(app);
        var listRequests = app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count();

        var cancelled = page.DeleteCommand.ExecuteAsync(Cbc);
        Dispatcher.UIThread.RunJobs();
        var confirm = window.GetVisualDescendants().OfType<ConfirmDialog>().Single();
        Assert.Contains(Texts(window), t => t == "Delete the test \"Complete blood count\"?");
        confirm.Close(false);
        await cancelled;
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Delete, TestsPath));

        var delete = page.DeleteCommand.ExecuteAsync(Cbc);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.Api.RequestsTo(HttpMethod.Delete, $"{TestsPath}/{Cbc.Id}"));
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count() > listRequests);
        Assert.Contains("Lab test deleted.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Editing_a_test_that_was_deleted_meanwhile_says_so()
    {
        using var app = AdminWithTests(Cbc);
        app.Api.On(HttpMethod.Get, $"{TestsPath}/{Cbc.Id}", _ => FakeApiHandler.Problem(HttpStatusCode.NotFound, ErrorCodes.NotFound));
        var (page, window) = await ShowPage(app);

        await page.EditCommand.ExecuteAsync(Cbc);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.Contains("This record no longer exists.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task A_failed_load_shows_the_error_state()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.FailWithNetworkError();

        var (page, window) = await ShowPage(app);

        Assert.True(page.HasLoadError);
        Assert.True(window.GetVisualDescendants().OfType<ErrorState>().Single().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task In_arabic_both_views_are_translated_and_right_to_left()
    {
        using var app = AdminWithTests(Cbc, Sugar);
        var (page, window) = await ShowPage(app);

        app.Localization.SetLanguage(AppLanguage.Arabic);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, window.FlowDirection);
        Assert.Contains("التحاليل", Texts(window));
        Assert.Contains("غير متاح", window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text));

        page.ShowGridCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("تكلفة المعمل", Texts(window));
        Assert.Equal(2, VisibleCards(window).Count());
    }

    private static TestServices AdminWithTests(params LabTestDto[] tests)
    {
        var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(TestsPath, new PagedResult<LabTestDto>(tests, 1, 20, tests.Length));
        return app;
    }

    /// <summary>The page inside a DialogHost (attached to the DialogService) with toasts attached to the window.</summary>
    private static async Task<(LabTestsViewModel Page, Window Window)> ShowPage(TestServices app)
    {
        var page = app.Get<LabTestsViewModel>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        app.Get<ToastService>().Attach(window);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }

    private static LabTestFormViewModel OpenAddForm(LabTestsViewModel page, Window window)
    {
        _ = page.AddCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        return CurrentForm(window);
    }

    private static LabTestFormViewModel CurrentForm(Window window) =>
        (LabTestFormViewModel)window.GetVisualDescendants().OfType<Modal>().Last().Content!;

    private static DataGrid Table(Window window) => window.GetVisualDescendants().OfType<DataGrid>().Single();

    private static IEnumerable<Card> VisibleCards(Window window) =>
        window.GetVisualDescendants().OfType<Card>().Where(c => c.IsEffectivelyVisible);

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static IEnumerable<IconButton> RowButtons(Visual container) =>
        container.GetVisualDescendants().OfType<IconButton>().Where(b => b.CommandParameter is LabTestDto);

    private static string? FieldError(Window window, string label) =>
        window.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == label && f.IsEffectivelyVisible).Error;

    private static List<string?> Toasts(Window window) =>
        window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message).ToList();
}

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.LabTests;

[Collection(UiTestCollection.Name)]
public class LabTestsPageTests : UiTestBase
{
    private const string TestsPath = "/api/lab-tests";

    private static readonly LabTestDto Cbc =
        new(Guid.NewGuid(), "CBC", "Complete blood count", 9000, true, DateTime.UtcNow, null, null, 2);

    private static readonly LabTestDto Sugar =
        new(Guid.NewGuid(), "FBS", "تحليل السكر الصائم", 2500, false, DateTime.UtcNow, null, null, 0);

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
    public async Task The_table_lists_each_test_with_its_lab_cost_status_and_row_actions()
    {
        using var app = AdminWithTests(Cbc, Sugar);

        var (page, window) = await ShowPage(app);

        Assert.False(page.IsGridView);
        Assert.True(Table(window).IsEffectivelyVisible);
        Assert.Empty(VisibleCards(window));
        var texts = Texts(window);
        Assert.Contains("Complete blood count", texts);
        Assert.Contains("CBC", texts);
        Assert.Contains("90.00", texts);
        Assert.Contains("25.00", texts);
        Assert.DoesNotContain("150.50", texts);
        Assert.Contains("2 tests", texts);
        Assert.Contains("Unavailable", window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text));
        Assert.Equal(2, RowButtons(Table(window)).Count(b => b.Command == page.EditCommand));
        Assert.Equal(2, RowButtons(Table(window)).Count(b => b.Command == page.DeleteCommand));
        var numbers = window.GetVisualDescendants().OfType<RowNumber>().ToDictionary(n => ((LabTestDto)n.DataContext!).Id, n => n.Text);
        Assert.Equal(("1", "2"), (numbers[Cbc.Id], numbers[Sugar.Id]));
        var copies = Table(window).GetVisualDescendants().OfType<CopyButton>().Select(b => b.Text).ToList();
        Assert.Equal<string?>(["CBC", "Complete blood count", "FBS", Sugar.Name], copies.Order(StringComparer.Ordinal));
    }

    [AvaloniaFact]
    public async Task The_grid_shows_a_card_per_test_with_edit_and_delete()
    {
        using var app = AdminWithTests(Cbc, Sugar);
        var (page, window) = await ShowPage(app);

        page.ShowGridCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Table(window).IsEffectivelyVisible);
        var cards = window.GetVisualDescendants().OfType<Card>().ToList();
        Assert.Equal<string?>([Cbc.Name, Sugar.Name], cards.Select(c => c.Title));
        Assert.All(cards, card =>
            Assert.Equal<object?>([page.EditCommand, page.DeleteCommand], RowButtons(card).Select(b => b.Command)));
        var cardTexts = cards.SelectMany(c => c.GetVisualDescendants().OfType<TextBlock>()).Select(t => t.Text).ToList();
        Assert.Contains("90.00", cardTexts);
        Assert.Contains("25.00", cardTexts);
        Assert.Contains("selected", Named<Button>(window, "GridViewButton").Classes);
        Assert.DoesNotContain("selected", Named<Button>(window, "TableViewButton").Classes);

        page.ShowTableCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Table(window).IsEffectivelyVisible);
        Assert.Empty(VisibleCards(window));
    }

    [AvaloniaTheory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Arabic)]
    public async Task The_search_box_is_centered_in_the_toolbar_and_takes_half_of_it(AppLanguage language)
    {
        using var app = AdminWithTests(Cbc);
        app.Localization.SetLanguage(language);
        var (_, window) = await ShowPage(app);

        var search = Named<SearchBox>(window, "SearchBox");
        var toolbar = (Grid)search.Parent!;
        var start = search.TranslatePoint(default, toolbar)!.Value.X;
        var end = toolbar.Bounds.Width - start - search.Bounds.Width;

        Assert.InRange(start - end, -1, 1);
        Assert.True(search.Bounds.Width >= toolbar.Bounds.Width / 2 - 10, $"{search.Bounds.Width} of {toolbar.Bounds.Width}");
        Assert.Equal(PageQuery.MaxSearchLength, search.MaxLength);
        Assert.Equal(app.Localization.Get("LabTests.SearchPlaceholder"), search.PlaceholderText);
    }

    [AvaloniaFact]
    public async Task The_list_asks_for_40_tests_per_page()
    {
        using var app = AdminWithTests(Cbc);

        var (page, _) = await ShowPage(app);

        Assert.Equal(40, page.PageSize);
        Assert.Contains("pageSize=40", app.Api.RequestsTo(HttpMethod.Get, TestsPath).Single().RequestUri!.Query);
    }

    [AvaloniaFact]
    public async Task Export_reads_every_test_whatever_the_search_and_saves_a_pdf_where_the_user_chose()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var catalog = Enumerable.Range(1, 3)
            .Select(i => Cbc with { Id = Guid.NewGuid(), Code = $"T{i:000}", Name = $"Test {i:000}" })
            .ToList();
        app.Api.OnGet(TestsPath, new PagedResult<LabTestDto>([Cbc], 1, 40, 1));
        app.Api.OnGet($"{TestsPath}/all", (IReadOnlyList<LabTestDto>)catalog);
        var (page, _) = await ShowPage(app);
        page.Search = "T001";
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "chosen", "my-tests.pdf");

        await page.ExportCommand.ExecuteAsync(null);

        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, $"{TestsPath}/all"));
        Assert.Equal(["lab-tests.pdf"], app.SaveSuggestions);
        Assert.Empty(app.OpenedFiles);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(app.PickedSavePath), 0, 4));
        Assert.False(page.IsExporting);
    }

    [AvaloniaFact]
    public async Task Cancelling_the_save_dialog_exports_nothing_and_asks_the_server_for_nothing()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(TestsPath, new PagedResult<LabTestDto>([Cbc], 1, 40, 1));
        var (page, _) = await ShowPage(app);
        app.PickedSavePath = null;

        await page.ExportCommand.ExecuteAsync(null);

        Assert.Equal(["lab-tests.pdf"], app.SaveSuggestions);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, $"{TestsPath}/all"));
        Assert.Empty(app.OpenedFiles);
    }

    [AvaloniaFact]
    public async Task Import_uploads_the_chosen_file_reloads_and_toasts_the_counts()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(TestsPath, new PagedResult<LabTestDto>([Cbc], 1, 40, 1));
        app.Api.On(HttpMethod.Post, $"{TestsPath}/import", _ =>
            FakeApiHandler.Json(new ImportLabTestsResult(2, 1, [new ImportLabTestSkipDto(3, "Dup", ErrorCodes.Taken)])));
        var (page, window) = await ShowPage(app);
        var importFile = Path.Combine(app.DownloadsFolder, "tests.csv");
        Directory.CreateDirectory(app.DownloadsFolder);
        await File.WriteAllTextAsync(importFile, "Name,Price\nA,$1.00\n");
        app.PickedOpenPath = importFile;
        var listRequests = app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count();

        await page.ImportCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Import lab tests"], app.OpenPickerTitles);
        Assert.Single(app.Api.RequestsTo(HttpMethod.Post, $"{TestsPath}/import"));
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count() > listRequests);
        Assert.Contains("Imported 2 tests. Skipped 1 rows.", Toasts(window));
        Assert.False(page.IsImporting);
    }

    [AvaloniaFact]
    public async Task Cancelling_the_import_file_dialog_uploads_nothing()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(TestsPath, new PagedResult<LabTestDto>([Cbc], 1, 40, 1));
        var (page, _) = await ShowPage(app);
        app.PickedOpenPath = null;

        await page.ImportCommand.ExecuteAsync(null);

        Assert.Equal(["Import lab tests"], app.OpenPickerTitles);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, $"{TestsPath}/import"));
    }

    [AvaloniaFact]
    public async Task Download_template_saves_the_csv_where_the_user_chose()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(TestsPath, new PagedResult<LabTestDto>([Cbc], 1, 40, 1));
        app.Api.On(HttpMethod.Get, $"{TestsPath}/import-template", _ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("Name,Price\r\n")),
            });
        var (page, window) = await ShowPage(app);
        Directory.CreateDirectory(app.DownloadsFolder);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "template.csv");

        await page.DownloadTemplateCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["lab-tests-import-template.csv"], app.SaveSuggestions);
        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, $"{TestsPath}/import-template"));
        Assert.True(File.Exists(app.PickedSavePath));
        Assert.Contains(app.PickedSavePath, Toasts(window).Single()!);
    }

    [AvaloniaFact]
    public async Task The_export_button_shows_a_spinner_while_the_pdf_is_prepared()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var release = new TaskCompletionSource();
        app.Api.OnGet(TestsPath, new PagedResult<LabTestDto>([Cbc], 1, 40, 1));
        app.Api.On(HttpMethod.Get, $"{TestsPath}/all", async (_, _) =>
        {
            await release.Task;
            return FakeApiHandler.Json((IReadOnlyList<LabTestDto>)[Cbc]);
        });
        var (page, window) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "spin.pdf");

        var running = page.ExportCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ExportButton");
        Assert.True(page.IsExporting);
        Assert.False(page.ExportCommand.CanExecute(null));
        Assert.Contains(button.GetVisualDescendants().OfType<Mostawsef.Desktop.Components.Spinner>(), s => s.IsEffectivelyVisible);

        release.SetResult();
        await running;
        Dispatcher.UIThread.RunJobs();
        Assert.False(page.IsExporting);
        Assert.DoesNotContain(button.GetVisualDescendants().OfType<Mostawsef.Desktop.Components.Spinner>(), s => s.IsEffectivelyVisible);
        Assert.True(File.Exists(app.PickedSavePath));
    }

    [AvaloniaFact]
    public async Task Export_while_the_server_is_down_says_so_and_opens_nothing()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(TestsPath, new PagedResult<LabTestDto>([Cbc], 1, 40, 1));
        app.Api.On(HttpMethod.Get, $"{TestsPath}/all", _ => throw new HttpRequestException("Connection refused"));
        var (page, window) = await ShowPage(app);
        app.PickedSavePath = Path.Combine(app.DownloadsFolder, "down.pdf");

        await page.ExportCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(app.OpenedFiles);
        Assert.False(File.Exists(app.PickedSavePath));
        Assert.Contains(app.Localization.Get("Login.ServerUnreachable"), Toasts(window));
    }

    [AvaloniaFact]
    public async Task Edit_on_a_card_opens_the_form_filled_in_and_saves_the_lab_cost_in_minor_units()
    {
        using var app = AdminWithTests(Cbc);
        UpdateLabTestRequest? sent = null;
        app.Api.On(HttpMethod.Put, $"{TestsPath}/{Cbc.Id}", request =>
        {
            sent = request.Content!.ReadFromJsonAsync<UpdateLabTestRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(Cbc with
            {
                LabCost = sent!.LabCost!.Value, IsActive = sent.IsActive, Version = 3,
            });
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
        Assert.Equal(("CBC", "Complete blood count", 90.00m, true), (form.Code, form.Name, form.LabCost, form.IsActive));
        Assert.Equal("Edit lab test", window.GetVisualDescendants().OfType<Modal>().Single().Title);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, $"{TestsPath}/{Cbc.Id}"));

        form.LabCost = 120.25m;
        form.IsActive = false;
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(
            new UpdateLabTestRequest { Code = "CBC", Name = "Complete blood count", LabCost = 12025, IsActive = false, Version = Cbc.Version },
            sent);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.Equal(listRequests, app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count()); // edit patches the row; no list reload
        Assert.Equal((12025L, false, 3), (page.Items.Single().LabCost, page.Items.Single().IsActive, page.Items.Single().Version));
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
        Assert.Equal("This field is required.", FieldError(window, "Lab cost"));
        Assert.False(Named<CheckBox>(window, "IsActiveBox").IsEffectivelyVisible);
        app.Localization.SetLanguage(AppLanguage.Arabic);
        Assert.Equal("هذا الحقل مطلوب.", FieldError(window, "تكلفة المعمل"));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, TestsPath));
    }

    [AvaloniaFact]
    public async Task Values_past_the_shared_limits_are_refused_before_calling_the_api()
    {
        using var app = AdminWithTests();
        var form = new LabTestFormViewModel(app.Get<LabTestsApi>(), app.Get<IDialogService>(), app.Localization)
        {
            Code = new string('C', LabTestRules.CodeMaxLength + 1),
            Name = new string('n', LabTestRules.NameMaxLength + 1),
            LabCost = Money.ToMajor(LabTestRules.MaxAmount) + 1,
        };

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal($"max_length|{LabTestRules.CodeMaxLength}", form.Errors[nameof(form.Code)]);
        Assert.Equal($"max_length|{LabTestRules.NameMaxLength}", form.Errors[nameof(form.Name)]);
        Assert.Equal($"range|0|{LabTestRules.MaxAmount}", form.Errors[nameof(form.LabCost)]);
        Assert.Equal("This value is outside the allowed range.", app.Localization.Get("ErrorCode.range"));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, TestsPath));
    }

    [AvaloniaFact]
    public async Task Form_inputs_have_placeholders_and_the_same_limits_as_the_api()
    {
        using var app = AdminWithTests();
        var (page, window) = await ShowPage(app);
        var form = OpenAddForm(page, window);

        var name = Named<TextBox>(window, "NameBox");
        var code = Named<TextBox>(window, "CodeBox");
        var labCost = Named<NumericUpDown>(window, "LabCostBox");
        Assert.Equal(("e.g. Complete blood count", LabTestRules.NameMaxLength), (name.PlaceholderText, name.MaxLength));
        Assert.Equal(("e.g. CBC", LabTestRules.CodeMaxLength), (code.PlaceholderText, code.MaxLength));
        Assert.Equal("0.00", labCost.PlaceholderText);
        Assert.Equal(0, labCost.Minimum);
        Assert.Equal(Money.ToMajor(LabTestRules.MaxAmount), labCost.Maximum);
        Assert.Contains("What the lab charges the clinic, up to 1000000.", window.GetVisualDescendants().OfType<FormField>().Select(f => f.Hint));

        var typed = labCost.GetVisualDescendants().OfType<TextBox>().Single();
        typed.Focus();
        window.KeyTextInput("2000000");
        window.KeyPressQwerty(Avalonia.Input.PhysicalKey.Enter, Avalonia.Input.RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Money.ToMajor(LabTestRules.MaxAmount), form.LabCost);
    }

    [AvaloniaFact]
    public async Task A_new_test_is_sent_trimmed_in_minor_units_then_the_list_inserts()
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
        form.LabCost = 25.5m;
        await form.SaveCommand.ExecuteAsync(null);
        await add;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new CreateLabTestRequest { Code = "fbs", Name = "Fasting blood sugar", LabCost = 2550 }, sent);
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.Equal(listRequests, app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count());
        Assert.Contains(page.Items, t => t.Id == Sugar.Id);
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
    public async Task Deleting_a_test_used_by_orders_is_refused_with_a_hint_to_deactivate_it()
    {
        using var app = AdminWithTests(Cbc);
        app.Api.On(HttpMethod.Delete, $"{TestsPath}/{Cbc.Id}", _ => FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.InUse));
        var (page, window) = await ShowPage(app);

        var delete = page.DeleteCommand.ExecuteAsync(Cbc);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("This test is used by orders, so it can't be deleted. Deactivate it instead.", Toasts(window));
        Assert.Single(page.Items);
    }

    [AvaloniaFact]
    public async Task Saving_an_edit_of_a_test_that_was_deleted_meanwhile_says_so()
    {
        using var app = AdminWithTests(Cbc);
        app.Api.On(HttpMethod.Put, $"{TestsPath}/{Cbc.Id}", _ => FakeApiHandler.Problem(HttpStatusCode.NotFound, ErrorCodes.NotFound));
        var (page, window) = await ShowPage(app);

        // Do not await EditCommand: ShowFormAsync stays open until the form closes.
        _ = page.EditCommand.ExecuteAsync(Cbc);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm(window);
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("ErrorCode.not_found", form.FormErrorKey);
        Assert.Equal("This record no longer exists.", app.Localization.Get(form.FormErrorKey!));
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

        Assert.Contains("متاح", window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text));
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

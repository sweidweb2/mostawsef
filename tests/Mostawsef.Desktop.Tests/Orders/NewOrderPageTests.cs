using System.Net;
using System.Net.Http.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Orders;

public class NewOrderPageTests
{
    private static readonly string[] Columns = ["PatientColumn", "TestsColumn", "CatalogColumn"];

    [AvaloniaFact]
    public async Task The_catalog_is_read_once_and_offers_only_active_tests()
    {
        using var app = Admin();
        var (shell, page, window) = await ShowNewOrderAsync(app);

        page.Deactivate();
        await page.ActivateAsync();

        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Contains("active=true", app.Api.RequestsTo(HttpMethod.Get, TestsPath).Single().RequestUri!.Query);
        Assert.Equal([Cbc.Id, Sugar.Id], page.Catalog.Select(t => t.Test.Id));
        Assert.Equal(2, CatalogCards(window).Count());
        Assert.Same(page, shell.CurrentPage);
    }

    [AvaloniaFact]
    public async Task Searching_the_catalog_asks_the_server_after_a_short_delay()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);

        page.CatalogSearch = "سُكَّر";
        await WaitUntil(() => page.Catalog.Count == 1 && page.Catalog[0].Test.Id == Sugar.Id);
        Assert.Equal([Sugar.Id], CatalogCards(window).Select(c => ((CatalogTestItem)c.DataContext!).Test.Id));

        page.CatalogSearch = "cbc";
        await WaitUntil(() => page.Catalog.Count == 1 && page.Catalog[0].Test.Id == Cbc.Id);
        Assert.Equal([Cbc.Id], CatalogCards(window).Select(c => ((CatalogTestItem)c.DataContext!).Test.Id));

        page.CatalogSearch = "nothing like it";
        await WaitUntil(() => page.IsCatalogEmpty);
        Assert.Empty(CatalogCards(window));
        Assert.True(Named<EmptyState>(window, "CatalogEmptyState").IsEffectivelyVisible);

        page.CatalogSearch = "";
        await WaitUntil(() => page.Catalog.Count == 2);
        Assert.Equal(2, CatalogCards(window).Count());
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count() >= 4);
    }

    [AvaloniaFact]
    public async Task Clicking_a_card_adds_the_test_once_and_leaves_a_ghost_until_it_is_removed()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        var cbc = page.Catalog.Single(t => t.Test.Id == Cbc.Id);

        Card(window, Cbc.Id).Command!.Execute(cbc);
        page.AddTestCommand.Execute(cbc);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([Cbc.Id], page.SelectedTests.Select(t => t.Test.Id));
        Assert.False(Card(window, Cbc.Id).IsEffectivelyEnabled);
        Assert.True(Card(window, Cbc.Id).IsEffectivelyVisible);
        Assert.Contains("Selected tests (1)", Texts(window));

        page.RemoveTestCommand.Execute(page.SelectedTests.Single());
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(page.SelectedTests);
        Assert.True(Card(window, Cbc.Id).IsEffectivelyEnabled);
        Assert.True(Named<EmptyState>(window, "NoTestsState").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task The_total_uses_the_catalog_sum_until_our_price_is_typed_and_shows_the_balance_after_a_payment()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        AddTests(page, Cbc, Sugar);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(20000, page.CatalogTotal);
        Assert.Equal(20000, page.Total);
        Assert.Equal("200.00", Named<TextBlock>(window, "TotalText").Text);
        Assert.False(Named<TextBlock>(window, "RemainingText").IsEffectivelyVisible);

        page.OurPrice = 170m;
        page.PaymentAmount = 50m;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(20000, page.CatalogTotal);
        Assert.Equal(17000, page.Total);
        Assert.Equal("200.00", Named<TextBlock>(window, "TotalText").Text);
        Assert.Equal("120.00", Named<TextBlock>(window, "RemainingText").Text);
        Assert.True(Named<TextBlock>(window, "RemainingText").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Create_needs_a_patient_and_at_least_one_test()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);

        Assert.False(page.SaveCommand.CanExecute(null));
        AddTests(page, Cbc);
        Assert.False(page.SaveCommand.CanExecute(null));

        page.SelectPatientCommand.Execute(Mona);
        Assert.True(page.SaveCommand.CanExecute(null));

        page.ChooseNewPatientCommand.Execute(null);
        Assert.False(page.SaveCommand.CanExecute(null));
        page.PatientForm.FullName = "Omar";
        page.PatientForm.Phone = "0111";
        page.PatientForm.Gender = Genders.Male;
        Assert.True(page.SaveCommand.CanExecute(null));

        page.RemoveTestCommand.Execute(page.SelectedTests.Single());
        Assert.False(page.SaveCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Patient_search_hits_the_api_and_picking_one_hides_the_box()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);

        Assert.Empty(page.Patients);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, PatientsPath));

        page.PatientSearch = "mona";
        await WaitUntil(() => !page.IsLoadingPatients && page.Patients.Count > 0);

        Assert.Equal(Mona.Id, Assert.Single(page.Patients).Id);
        var search = Assert.Single(app.Api.RequestsTo(HttpMethod.Get, PatientsPath));
        Assert.Contains("search=mona", search.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains($"pageSize={PageQuery.DefaultPageSize}", search.RequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("includeTotal=false", search.RequestUri.Query, StringComparison.Ordinal);
        Assert.False(page.NoPatientsFound);

        page.PatientPick = Mona;
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<Border>(window, "SelectedPatientCard").IsEffectivelyVisible);
        Assert.False(Named<AutoCompleteBox>(window, "PatientSearchBox").IsEffectivelyVisible);

        page.ChangePatientCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Named<AutoCompleteBox>(window, "PatientSearchBox").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task An_unknown_patient_search_says_so()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);

        page.PatientSearch = "nobody";
        await WaitUntil(() => !page.IsLoadingPatients && page.NoPatientsFound);

        Assert.Empty(page.Patients);
        Assert.Contains("No patient matches. Check the spelling or add a new patient.", Texts(window));
        Assert.Contains("search=nobody", Assert.Single(app.Api.RequestsTo(HttpMethod.Get, PatientsPath)).RequestUri!.Query, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Arabic_and_eastern_digits_reach_the_api_as_the_typed_search()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);

        page.PatientSearch = "٠١٠";
        await WaitUntil(() => !page.IsLoadingPatients && page.Patients.Count > 0);
        Assert.Equal(Mona.Id, Assert.Single(page.Patients).Id);

        page.PatientSearch = "خالد";
        await WaitUntil(() => !page.IsLoadingPatients && page.Patients.Any(p => p.Id == Omar.Id));
        Assert.Equal(Omar.Id, Assert.Single(page.Patients).Id);
    }

    [AvaloniaFact]
    public async Task A_patient_search_shorter_than_three_letters_does_not_call_the_api()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);

        page.PatientSearch = "mo";
        await Task.Delay(NewOrderViewModel.PatientSearchDelay + TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, PatientsPath));
        Assert.False(page.NoPatientsFound);
        Assert.Empty(page.Patients);
    }

    [AvaloniaFact]
    public async Task New_patient_and_payment_errors_show_without_calling_the_api()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        AddTests(page, Sugar);
        page.ChooseNewPatientCommand.Execute(null);
        page.PatientForm.FullName = "Omar";
        page.PatientForm.Phone = "call me";
        page.PatientForm.Gender = Genders.Male;
        page.PaymentAmount = 60m;

        await page.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Invalid, page.PatientForm.Errors[nameof(PatientFormViewModel.Phone)]);
        Assert.Equal(ErrorCodes.ExceedsTotal, page.Errors[nameof(page.PaymentAmount)]);
        Assert.Equal("The payment can't be more than the order total.", FieldError(window, "Amount"));
        Assert.Equal("Some fields need attention.", Named<Alert>(window, "FormError").Text);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, OrdersPath));
    }

    [AvaloniaFact]
    public async Task A_valid_order_is_sent_with_empty_our_price_as_null_then_its_details_open()
    {
        using var app = Admin();
        var created = Details(Guid.NewGuid());
        CreateOrderRequest? sent = null;
        app.Api.On(HttpMethod.Post, OrdersPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<CreateOrderRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(created);
        });
        app.Api.OnGet($"{OrdersPath}/{created.Id}", created);
        var (shell, page, window) = await ShowNewOrderAsync(app);
        AddTests(page, Cbc, Sugar);
        page.OurPrice = 180m;
        page.ChooseNewPatientCommand.Execute(null);
        page.PatientForm.FullName = "  Omar Khaled ";
        page.PatientForm.Phone = " 0111 ";
        page.PatientForm.Gender = Genders.Male;
        page.PaymentAmount = 50m;
        page.PaymentMethod = PaymentMethods.Card;
        page.PaymentNotes = "  ";

        await page.SaveCommand.ExecuteAsync(null);
        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasOrder);

        Assert.NotNull(sent);
        Assert.Null(sent.PatientId);
        Assert.Equal(("Omar Khaled", "0111", Genders.Male), (sent.NewPatient!.FullName, sent.NewPatient.Phone, sent.NewPatient.Gender));
        Assert.Equal(
            [new CreateOrderItemRequest { LabTestId = Cbc.Id }, new CreateOrderItemRequest { LabTestId = Sugar.Id }],
            sent.Items);
        Assert.Equal(18000, sent.OurPrice);
        Assert.Equal(new CreateFirstPaymentRequest { Amount = 5000, Method = PaymentMethods.Card, Notes = null }, sent.FirstPayment);
        Assert.Equal(created.Id, details.OrderId);
        Assert.Contains("Order ORD-000007 created.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Server_field_errors_land_on_the_patient_form_the_test_and_the_payment()
    {
        using var app = Admin();
        app.Api.On(HttpMethod.Post, OrdersPath, _ => FakeApiHandler.Problem(
            HttpStatusCode.BadRequest,
            ErrorCodes.Validation,
            ("NewPatient.Phone", ErrorCodes.Invalid),
            ("Items[1].LabTestId", ErrorCodes.Inactive),
            ("FirstPayment.Amount", ErrorCodes.ExceedsTotal)));
        var (_, page, window) = await ShowNewOrderAsync(app);
        AddTests(page, Cbc, Sugar);
        page.ChooseNewPatientCommand.Execute(null);
        page.PatientForm.FullName = "Omar";
        page.PatientForm.Phone = "0111";
        page.PatientForm.Gender = Genders.Male;
        page.PaymentAmount = 10m;

        await page.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Invalid, page.PatientForm.Errors[nameof(PatientFormViewModel.Phone)]);
        Assert.Null(page.SelectedTests[0].Errors[nameof(CreateOrderItemRequest.LabTestId)]);
        Assert.Equal(ErrorCodes.Inactive, page.SelectedTests[1].Errors[nameof(CreateOrderItemRequest.LabTestId)]);
        Assert.Contains("This test is no longer offered.", Texts(window));
        Assert.Equal(ErrorCodes.ExceedsTotal, page.Errors[nameof(page.PaymentAmount)]);
        Assert.Equal("ErrorCode.validation", page.FormErrorKey);
    }

    [AvaloniaTheory]
    [InlineData(false, "Login.ServerUnreachable")]
    [InlineData(true, "Error.RequestFailed")]
    public async Task A_server_failure_shows_the_message_on_top(bool serverError, string key)
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);
        if (serverError)
        {
            app.Api.On(HttpMethod.Post, OrdersPath, HttpStatusCode.InternalServerError);
        }
        else
        {
            app.Api.On(HttpMethod.Post, OrdersPath, _ => throw new HttpRequestException("Connection refused"));
        }

        AddTests(page, Cbc);
        page.SelectPatientCommand.Execute(Mona);

        await page.SaveCommand.ExecuteAsync(null);

        Assert.Equal(key, page.FormErrorKey);
    }

    [AvaloniaFact]
    public async Task Back_with_changes_asks_before_discarding_them()
    {
        using var app = Admin();
        var (shell, page, window) = await ShowNewOrderAsync(app);
        AddTests(page, Cbc);
        Assert.True(page.IsDirty);

        var keep = page.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(false);
        await keep;
        Assert.Same(page, shell.CurrentPage);

        var discard = page.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await discard;
        Assert.IsType<OrdersViewModel>(shell.CurrentPage);
    }

    [AvaloniaFact]
    public async Task Another_menu_page_asks_first_and_keeping_the_changes_keeps_the_menu_on_orders()
    {
        using var app = Admin();
        var (shell, page, window) = await ShowNewOrderAsync(app);
        var orders = shell.SelectedMenuIndex;
        AddTests(page, Cbc);

        shell.SelectedMenuIndex = 0;
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(false);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(page, shell.CurrentPage);
        Assert.Equal(orders, shell.SelectedMenuIndex);

        shell.SelectedMenuIndex = 0;
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        Dispatcher.UIThread.RunJobs();

        Assert.IsType<ViewModels.Shell.DashboardViewModel>(shell.CurrentPage);
        Assert.Null(shell.SubPage);
    }

    [AvaloniaFact]
    public async Task An_untouched_page_closes_without_asking()
    {
        using var app = Admin();
        var (shell, page, window) = await ShowNewOrderAsync(app);
        page.CatalogSearch = "cbc";

        await page.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(page.IsDirty);
        Assert.Empty(window.GetVisualDescendants().OfType<ConfirmDialog>());
        Assert.IsType<OrdersViewModel>(shell.CurrentPage);
    }

    [AvaloniaTheory]
    [InlineData(AppLanguage.English, "New order", "Selected tests (1)")]
    [InlineData(AppLanguage.Arabic, "طلب جديد", "التحاليل المختارة (1)")]
    public async Task The_three_columns_render_side_by_side_in_both_languages(AppLanguage language, string title, string testsTitle)
    {
        using var app = Admin(language);
        var (_, page, window) = await ShowNewOrderAsync(app);
        AddTests(page, Cbc);
        Dispatcher.UIThread.RunJobs();

        var texts = Texts(window);
        Assert.Contains(title, texts);
        Assert.Contains(testsTitle, texts);
        Assert.False(Named<TabStrip>(window, "ColumnTabs").IsEffectivelyVisible);
        var columns = Columns.Select(name => Named<Card>(window, name)).ToList();
        Assert.All(columns, c => Assert.True(c.IsEffectivelyVisible));
        var x = columns.Select(c => c.TranslatePoint(default, window)!.Value.X).ToList();
        if (language == AppLanguage.Arabic)
        {
            Assert.True(x[0] > x[1] && x[1] > x[2], string.Join(", ", x));
        }
        else
        {
            Assert.True(x[0] < x[1] && x[1] < x[2], string.Join(", ", x));
        }
    }

    [AvaloniaFact]
    public async Task At_the_minimum_window_size_the_columns_become_tabs()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app, width: 720, height: 520);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<TabStrip>(window, "ColumnTabs").IsEffectivelyVisible);
        Assert.Equal([true, false, false], VisibleColumns(window));
        Assert.True(Named<Button>(window, "CreateButton").IsEffectivelyVisible);

        page.SelectedTab = NewOrderViewModel.CatalogTab;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([false, false, true], VisibleColumns(window));
        Assert.True(Named<Button>(window, "CreateButton").IsEffectivelyVisible);
        Assert.Equal(Named<TabStrip>(window, "ColumnTabs").Bounds.Width, Named<Card>(window, "CatalogColumn").Bounds.Width, precision: 0);
    }

    private static void AddTests(NewOrderViewModel page, params Contracts.LabTests.LabTestDto[] tests)
    {
        foreach (var test in tests)
        {
            page.AddTestCommand.Execute(page.Catalog.Single(t => t.Test.Id == test.Id));
        }
    }

    private static IEnumerable<Button> CatalogCards(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("catalog-card"));

    private static Button Card(Window window, Guid testId) =>
        CatalogCards(window).Single(b => ((CatalogTestItem)b.DataContext!).Test.Id == testId);

    private static bool[] VisibleColumns(Window window) =>
        Columns.Select(name => Named<Card>(window, name).IsEffectivelyVisible).ToArray();

    private static string? FieldError(Window window, string label) =>
        window.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == label && f.IsEffectivelyVisible).Error;
}

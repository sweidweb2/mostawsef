using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Avalonia.Controls;
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
    [AvaloniaFact]
    public async Task Opening_the_page_does_not_load_the_catalog_until_the_tests_step()
    {
        using var app = Admin();
        var (shell, page, _) = await ShowNewOrderAsync(app);

        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Equal(NewOrderViewModel.PatientStep, page.SelectedStep);
        Assert.Same(page, shell.CurrentPage);

        page.SelectPatientCommand.Execute(Mona);
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => !page.IsCatalogLoading && page.Catalog.Count > 0);

        var catalog = Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Contains("active=true", catalog.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("includeTotal=false", catalog.RequestUri.Query, StringComparison.Ordinal);
        Assert.Equal([Cbc.Id, Sugar.Id], page.Catalog.Select(t => t.Test.Id));
    }

    [AvaloniaFact]
    public async Task Returning_to_the_tests_step_reuses_the_loaded_catalog()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);
        await GoToTestsAsync(page);
        AddTests(page, Cbc);

        page.NextStepCommand.Execute(null);
        Assert.Equal(NewOrderViewModel.ReviewStep, page.SelectedStep);
        page.PreviousStepCommand.Execute(null);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.TestsStep);

        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Equal(2, page.Catalog.Count);
    }

    [AvaloniaFact]
    public async Task Searching_the_catalog_asks_the_server_after_a_short_delay()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        await GoToTestsAsync(page);

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
        await GoToTestsAsync(page);
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
    public async Task The_total_adds_our_price_on_top_of_the_catalog_and_caps_the_payment_live()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        await GoToReviewAsync(page, Cbc, Sugar);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(20000, page.CatalogTotal);
        Assert.Equal(20000, page.Total);
        Assert.Equal(200m, page.MaxPayableAmount);
        Assert.Equal("200.00", Named<TextBlock>(window, "TotalText").Text);
        Assert.False(Named<TextBlock>(window, "RemainingText").IsEffectivelyVisible);

        page.OurPrice = 5m;
        page.PaymentAmount = 50m;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(20000, page.CatalogTotal);
        Assert.Equal(20500, page.Total);
        Assert.Equal(205m, page.MaxPayableAmount);
        Assert.Equal("205.00", Named<TextBlock>(window, "TotalText").Text);
        Assert.Equal("155.00", Named<TextBlock>(window, "RemainingText").Text);
        Assert.True(Named<TextBlock>(window, "RemainingText").IsEffectivelyVisible);
        Assert.Equal(205m, Named<NumericUpDown>(window, "PaymentAmountBox").Maximum);
    }

    [AvaloniaFact]
    public async Task Create_needs_a_patient_and_at_least_one_test_on_the_review_step()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);

        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.False(page.NextStepCommand.CanExecute(null));

        page.SelectPatientCommand.Execute(Mona);
        Assert.True(page.NextStepCommand.CanExecute(null));
        Assert.False(page.SaveCommand.CanExecute(null));

        await GoToTestsAsync(page);
        AddTests(page, Cbc);
        Assert.True(page.NextStepCommand.CanExecute(null));
        page.NextStepCommand.Execute(null);
        Assert.True(page.SaveCommand.CanExecute(null));

        page.PreviousStepCommand.Execute(null);
        page.PreviousStepCommand.Execute(null);
        page.ChooseNewPatientCommand.Execute(null);
        Assert.False(page.NextStepCommand.CanExecute(null));
        page.PatientForm.FullName = "Omar";
        page.PatientForm.Phone = "0111";
        page.PatientForm.Gender = Genders.Male;
        Assert.True(page.NextStepCommand.CanExecute(null));

        await GoToTestsAsync(page);
        page.RemoveTestCommand.Execute(page.SelectedTests.Single());
        Assert.False(page.NextStepCommand.CanExecute(null));
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
        page.ChooseNewPatientCommand.Execute(null);
        page.PatientForm.FullName = "Omar";
        page.PatientForm.Phone = "call me";
        page.PatientForm.Gender = Genders.Male;
        await GoToTestsAsync(page);
        AddTests(page, Sugar);
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.ReviewStep);
        page.PaymentAmount = 60m;

        await page.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Invalid, page.PatientForm.Errors[nameof(PatientFormViewModel.Phone)]);
        Assert.Equal(ErrorCodes.ExceedsTotal, page.Errors[nameof(page.PaymentAmount)]);
        Assert.Equal(NewOrderViewModel.PatientStep, page.SelectedStep);
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
        var (shell, page, window) = await ShowNewOrderAsync(app);
        page.ChooseNewPatientCommand.Execute(null);
        page.PatientForm.FullName = "  Omar Khaled ";
        page.PatientForm.Phone = " 0111 ";
        page.PatientForm.Gender = Genders.Male;
        await GoToReviewAsync(page, Cbc, Sugar);
        page.OurPrice = 5m;
        page.PaymentAmount = 50m;
        page.PaymentMethod = PaymentMethods.Card;
        page.PaymentNotes = "  ";

        await page.SaveCommand.ExecuteAsync(null);
        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasOrder && Toasts(window).Count > 0);

        Assert.True(details.HasOrder);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, $"{OrdersPath}/{created.Id}"));
        Assert.NotNull(sent);
        Assert.Null(sent.PatientId);
        Assert.Equal(("Omar Khaled", "0111", Genders.Male), (sent.NewPatient!.FullName, sent.NewPatient.Phone, sent.NewPatient.Gender));
        Assert.Equal(
            [new CreateOrderItemRequest { LabTestId = Cbc.Id }, new CreateOrderItemRequest { LabTestId = Sugar.Id }],
            sent.Items);
        Assert.Equal(500, sent.OurPrice);
        Assert.Equal(new CreateFirstPaymentRequest { Amount = 5000, Method = PaymentMethods.Card, Notes = null }, sent.FirstPayment);
        Assert.Equal(created.Id, details.OrderId);
        Assert.Equal(created.Number, details.Order!.Number);
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
        page.ChooseNewPatientCommand.Execute(null);
        page.PatientForm.FullName = "Omar";
        page.PatientForm.Phone = "0111";
        page.PatientForm.Gender = Genders.Male;
        await GoToReviewAsync(page, Cbc, Sugar);
        page.PaymentAmount = 10m;

        await page.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Invalid, page.PatientForm.Errors[nameof(PatientFormViewModel.Phone)]);
        Assert.Null(page.SelectedTests[0].Errors[nameof(CreateOrderItemRequest.LabTestId)]);
        Assert.Equal(ErrorCodes.Inactive, page.SelectedTests[1].Errors[nameof(CreateOrderItemRequest.LabTestId)]);
        Assert.Equal(ErrorCodes.ExceedsTotal, page.Errors[nameof(page.PaymentAmount)]);
        Assert.Equal("ErrorCode.validation", page.FormErrorKey);
        Assert.Equal(NewOrderViewModel.PatientStep, page.SelectedStep);
        Assert.Contains("Some fields need attention.", Texts(window));
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

        await GoToReviewAsync(page, Cbc);

        await page.SaveCommand.ExecuteAsync(null);

        Assert.Equal(key, page.FormErrorKey);
    }

    [AvaloniaFact]
    public async Task Back_with_changes_asks_before_discarding_them()
    {
        using var app = Admin();
        var (shell, page, window) = await ShowNewOrderAsync(app);
        await GoToTestsAsync(page);
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
        await GoToTestsAsync(page);
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
        page.SelectPatientCommand.Execute(Mona);
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.TestsStep && !page.IsCatalogLoading);
        page.CatalogSearch = "cbc";

        await page.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(page.IsDirty);
        Assert.Empty(window.GetVisualDescendants().OfType<ConfirmDialog>());
        Assert.IsType<OrdersViewModel>(shell.CurrentPage);
    }

    [AvaloniaTheory]
    [InlineData(AppLanguage.English, "New order", "1. Patient", "2. Tests", "3. Review")]
    [InlineData(AppLanguage.Arabic, "طلب جديد", "١. المريض", "٢. التحاليل", "٣. المراجعة")]
    public async Task The_step_indicator_renders_in_both_languages(
        AppLanguage language, string title, string patient, string tests, string review)
    {
        using var app = Admin(language);
        var (_, page, window) = await ShowNewOrderAsync(app);
        Dispatcher.UIThread.RunJobs();

        var texts = Texts(window);
        Assert.Contains(title, texts);
        Assert.Contains(patient, texts);
        Assert.Contains(tests, texts);
        Assert.Contains(review, texts);
        Assert.True(Named<ContentControl>(window, "StepHost").IsEffectivelyVisible);
        Assert.True(Named<Card>(window, "PatientColumn").IsEffectivelyVisible);
        Assert.Equal(NewOrderViewModel.PatientStep, page.SelectedStep);
    }

    [AvaloniaFact]
    public async Task At_the_minimum_window_size_only_the_active_step_is_built()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app, width: 720, height: 520);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<Card>(window, "PatientColumn").IsEffectivelyVisible);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Card>(), c => c.Name is "TestsColumn" or "CatalogColumn" or "ReviewColumn");
        Assert.True(Named<Button>(window, "NextStepButton").IsEffectivelyVisible);
        Assert.False(Named<Button>(window, "CreateButton").IsEffectivelyVisible);

        await GoToTestsAsync(page);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Card>(), c => c.Name == "PatientColumn");
        Assert.True(Named<Card>(window, "CatalogColumn").IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "NextStepButton").IsEffectivelyVisible);
    }

    private static async Task GoToTestsAsync(NewOrderViewModel page)
    {
        if (!page.HasPatient)
        {
            page.SelectPatientCommand.Execute(Mona);
        }

        if (page.SelectedStep < NewOrderViewModel.TestsStep)
        {
            page.NextStepCommand.Execute(null);
        }

        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.TestsStep && !page.IsCatalogLoading);
    }

    private static async Task GoToReviewAsync(NewOrderViewModel page, params Contracts.LabTests.LabTestDto[] tests)
    {
        await GoToTestsAsync(page);
        AddTests(page, tests);
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.ReviewStep);
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
}

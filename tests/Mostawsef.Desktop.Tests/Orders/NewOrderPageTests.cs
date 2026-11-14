using Avalonia;
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

[Collection(UiTestCollection.Name)]
public class NewOrderPageTests : UiTestBase
{
    [AvaloniaFact]
    public async Task Opening_the_page_does_not_load_the_catalog_until_the_tests_step()
    {
        using var app = Admin();
        var (shell, page, window) = await ShowNewOrderAsync(app);

        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Empty(page.Catalog);
        Assert.Equal(NewOrderViewModel.PatientStep, page.SelectedStep);
        Assert.Same(page, shell.CurrentPage);
        Assert.False(page.IsPreparing);
        Assert.False(Named<LoadingOverlay>(window, "PreparingOverlay").IsLoading);

        page.SelectPatientCommand.Execute(Mona);
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.TestsStep && !page.IsCatalogLoading);

        var catalog = Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Contains("active=true", catalog.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("includeTotal=false", catalog.RequestUri.Query, StringComparison.Ordinal);
        Assert.Contains($"pageSize={PageQuery.MaxPageSize}", catalog.RequestUri.Query, StringComparison.Ordinal);
        Assert.Equal([Cbc.Id, Sugar.Id], page.Catalog.Select(t => t.Test.Id));
    }

    [AvaloniaFact]
    public async Task Reopening_reuses_the_same_page_and_clears_the_previous_draft()
    {
        using var app = Admin();
        var (shell, orders, window) = await ShowOrdersPageAsync(app);
        await orders.NewOrderCommand.ExecuteAsync(null);
        var page = Assert.IsType<NewOrderViewModel>(shell.CurrentPage);
        await WaitUntil(() => !page.IsPreparing && page.StepContent is not null);

        page.SelectPatientCommand.Execute(Mona);
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.TestsStep && !page.IsCatalogLoading);
        AddTests(page, Cbc);
        Assert.True(page.IsDirty);

        var discard = page.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await discard;
        await WaitUntil(() => shell.CurrentPage == orders);
        Assert.Empty(page.Catalog);

        await orders.NewOrderCommand.ExecuteAsync(null);
        await WaitUntil(() => !page.IsPreparing && page.StepContent is not null);

        Assert.Same(page, shell.CurrentPage);
        Assert.Equal(NewOrderViewModel.PatientStep, page.SelectedStep);
        Assert.Null(page.SelectedPatient);
        Assert.Empty(page.SelectedTests);
        Assert.False(page.IsDirty);
        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
    }

    [AvaloniaFact]
    public async Task The_new_patient_form_is_built_only_after_choosing_new_patient()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(page.NewPatientFormContent);
        Assert.Null(Named<ContentControl>(window, "NewPatientForm").Content);

        page.ChooseNewPatientCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(page.PatientForm, page.NewPatientFormContent);
        Assert.Same(page.PatientForm, Named<ContentControl>(window, "NewPatientForm").Content);
        var fullName = window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "FullNameBox");

        // Switching back only hides the form; switching again shows the same view instead of rebuilding it.
        page.ChooseExistingPatientCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(Named<ContentControl>(window, "NewPatientForm").IsVisible);
        Assert.False(fullName.IsEffectivelyVisible);

        page.ChooseNewPatientCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(fullName.IsEffectivelyVisible);
        Assert.Same(fullName, window.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "FullNameBox"));
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
    public async Task A_second_order_uses_the_cached_catalog()
    {
        using var app = Admin();
        var (shell, orders, window) = await ShowOrdersPageAsync(app);
        await orders.NewOrderCommand.ExecuteAsync(null);
        var page = Assert.IsType<NewOrderViewModel>(shell.CurrentPage);
        await WaitUntil(() => !page.IsPreparing && page.StepContent is not null);
        await GoToTestsAsync(page);

        var discard = page.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await discard;
        await WaitUntil(() => shell.CurrentPage == orders);
        await orders.NewOrderCommand.ExecuteAsync(null);
        await WaitUntil(() => !page.IsPreparing && page.StepContent is not null);
        await GoToTestsAsync(page);

        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Equal([Cbc.Id, Sugar.Id], page.Catalog.Select(t => t.Test.Id));
        Assert.All(page.Catalog, t => Assert.False(t.IsSelected));
    }

    [AvaloniaFact]
    public async Task Catalog_reloads_after_lab_tests_are_mutated()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);
        await GoToTestsAsync(page);
        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));

        app.Get<LabTestCatalogVersion>().Invalidate();
        page.PreviousStepCommand.Execute(null);
        page.NextStepCommand.Execute(null);
        await WaitUntil(() => app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count() == 2);

        Assert.Equal(2, app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count());
    }

    [AvaloniaFact]
    public async Task Searching_the_catalog_filters_it_in_memory_without_asking_the_server()
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
        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
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
    public async Task The_total_adds_the_addon_on_top_of_the_catalog_and_caps_the_payment_live()
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

        page.Addon = 5m;
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
    public async Task Steps_are_free_to_move_but_create_needs_patient_and_tests()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);

        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.True(page.NextStepCommand.CanExecute(null));

        page.NextStepCommand.Execute(null);
        Assert.Equal(NewOrderViewModel.TestsStep, page.SelectedStep);
        Assert.Equal(OrderStepStatus.Missing, page.PatientStepItem.Status);
        Assert.Equal(ErrorCodes.Required, page.Errors[nameof(CreateOrderRequest.PatientId)]);

        page.GoToStepCommand.Execute(NewOrderViewModel.ReviewStep);
        Assert.Equal(NewOrderViewModel.ReviewStep, page.SelectedStep);
        Assert.Equal(OrderStepStatus.Missing, page.TestsStepItem.Status);
        Assert.Equal(ErrorCodes.Required, page.Errors[nameof(CreateOrderRequest.Items)]);
        Assert.True(page.SaveCommand.CanExecute(null));

        await page.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ErrorCode.validation", page.FormErrorKey);
        Assert.Equal(NewOrderViewModel.PatientStep, page.SelectedStep);
        Assert.Equal(OrderStepStatus.Missing, page.PatientStepItem.Status);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, OrdersPath));

        page.SelectPatientCommand.Execute(Mona);
        Assert.Equal(OrderStepStatus.Number, page.PatientStepItem.Status);
        Assert.True(page.IsPatientSatisfied);
        page.GoToStepCommand.Execute(NewOrderViewModel.TestsStep);
        await WaitUntil(() => page.SelectedStep == NewOrderViewModel.TestsStep && !page.IsCatalogLoading);
        AddTests(page, Cbc);
        Assert.True(page.IsTestsSatisfied);
        Assert.NotEqual(OrderStepStatus.Missing, page.TestsStepItem.Status);
        page.NextStepCommand.Execute(null);
        Assert.Equal(NewOrderViewModel.ReviewStep, page.SelectedStep);
        Assert.True(page.SaveCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Patient_search_hits_the_api_and_picking_one_hides_the_box()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);

        Assert.Empty(page.Patients);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, PatientsPath));

        await page.SearchPatientsAsync("mona");

        Assert.Equal(Mona.Id, Assert.Single(page.Patients).Id);
        var search = Assert.Single(app.Api.RequestsTo(HttpMethod.Get, PatientsPath));
        Assert.Contains("search=mona", search.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains($"pageSize={PageQuery.DefaultPageSize}", search.RequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("includeTotal=false", search.RequestUri.Query, StringComparison.Ordinal);
        Assert.False(page.NoPatientsFound);
        Assert.NotNull(Named<AutoCompleteBox>(window, "PatientSearchBox").AsyncPopulator);

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

        await page.SearchPatientsAsync("nobody");
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(page.Patients);
        Assert.True(page.NoPatientsFound);
        Assert.Contains("No patient matches. Check the spelling or add a new patient.", Texts(window));
        Assert.Contains("search=nobody", Assert.Single(app.Api.RequestsTo(HttpMethod.Get, PatientsPath)).RequestUri!.Query, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Arabic_and_eastern_digits_reach_the_api_as_the_typed_search()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);

        await page.SearchPatientsAsync("٠١٠");
        Assert.Equal(Mona.Id, Assert.Single(page.Patients).Id);

        await page.SearchPatientsAsync("خالد");
        Assert.Equal(Omar.Id, Assert.Single(page.Patients).Id);
    }

    [AvaloniaFact]
    public async Task A_patient_search_shorter_than_three_letters_does_not_call_the_api()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);

        await page.SearchPatientsAsync("mo");

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
    public async Task A_valid_order_is_sent_with_an_addon_then_its_details_open()
    {
        using var app = Admin();
        var created = Details(Guid.NewGuid()) with { Total = 20500, Paid = 5000, Balance = 15500, Addon = 500 };
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
        page.Addon = 5m;
        page.PaymentAmount = 50m;
        page.PaymentMethod = PaymentMethods.Card;
        page.PaymentNotes = "  ";

        await page.SaveCommand.ExecuteAsync(null);
        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasOrder && Toasts(window).Count > 0);

        Assert.True(details.HasOrder);
        Assert.True(details.HasAddon);
        Assert.Equal(500, details.Order!.Addon);
        Assert.True(Named<StackPanel>(window, "AddonRow").IsVisible);
        Assert.Equal("5.00", Named<TextBlock>(window, "AddonText").Text);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, $"{OrdersPath}/{created.Id}"));
        Assert.NotNull(sent);
        Assert.Null(sent.PatientId);
        Assert.Equal(("Omar Khaled", "0111", Genders.Male), (sent.NewPatient!.FullName, sent.NewPatient.Phone, sent.NewPatient.Gender));
        Assert.Equal(
            [new CreateOrderItemRequest { LabTestId = Cbc.Id }, new CreateOrderItemRequest { LabTestId = Sugar.Id }],
            sent.Items);
        Assert.Equal(500, sent.Addon);
        Assert.Equal(new CreateFirstPaymentRequest { Amount = 5000, Method = PaymentMethods.Card, Notes = null }, sent.FirstPayment);
        Assert.Equal(created.Id, details.OrderId);
        Assert.Equal(created.Number, details.Order.Number);
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

        // Moving between steps and searching the catalog are not changes (picking a patient would be one).
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
    [InlineData(AppLanguage.English, "Patient", "Tests", "Review")]
    [InlineData(AppLanguage.Arabic, "المريض", "التحاليل", "المراجعة")]
    public async Task The_step_indicator_renders_in_both_languages(
        AppLanguage language, string patient, string tests, string review)
    {
        using var app = Admin(language);
        var (_, page, window) = await ShowNewOrderAsync(app);
        Dispatcher.UIThread.RunJobs();

        var texts = Texts(window);
        Assert.Contains(patient, texts);
        Assert.Contains(tests, texts);
        Assert.Contains(review, texts);
        Assert.NotNull(window);
        Assert.True(Named<CachedContentHost>(window, "StepHost").IsEffectivelyVisible);
        Assert.True(Named<Card>(window, "PatientColumn").IsEffectivelyVisible);
        Assert.Equal(NewOrderViewModel.PatientStep, page.SelectedStep);

        var steps = Named<Grid>(window, "StepIndicator");
        var nodes = steps.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("step-node")).ToList();
        Assert.Equal(3, nodes.Count);
        Assert.All(nodes, node => Assert.True(node.IsEffectivelyVisible));
        // Four half-lines (two connectors); first has no left, last has no right.
        Assert.Equal(4, steps.GetVisualDescendants().OfType<Border>().Count(
            b => b.Classes.Contains("step-line") && b.IsVisible));
        Assert.Equal(3, steps.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("step-circle")));
    }

    [AvaloniaFact]
    public async Task At_the_minimum_window_size_only_the_active_step_is_visible()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app, width: 720, height: 520);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<Card>(window, "PatientColumn").IsEffectivelyVisible);
        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<Card>(),
            c => c.Name is "TestsColumn" or "CatalogColumn" or "ReviewColumn" && c.IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "NextStepButton").IsEffectivelyVisible);
        Assert.False(Named<Button>(window, "CreateButton").IsEffectivelyVisible);

        await GoToTestsAsync(page);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Named<Card>(window, "PatientColumn").IsEffectivelyVisible);
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

    [AvaloniaFact]
    public async Task Our_price_has_its_own_row_above_the_first_payment_fields()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        await GoToReviewAsync(page, Cbc);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var addon = Named<NumericUpDown>(window, "AddonBox");
        var amount = Named<NumericUpDown>(window, "PaymentAmountBox");
        var method = Named<ComboBox>(window, "PaymentMethodBox");

        // Not in the grid of the first payment, and above it (not beside it).
        Assert.Null(addon.FindAncestorOfType<ResponsiveGrid>());
        Assert.Same(amount.FindAncestorOfType<ResponsiveGrid>(), method.FindAncestorOfType<ResponsiveGrid>());
        var addonY = addon.TranslatePoint(default, window)!.Value.Y;
        var amountY = amount.TranslatePoint(default, window)!.Value.Y;
        Assert.True(addonY + addon.Bounds.Height <= amountY, $"our price at {addonY}, first payment at {amountY}");
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

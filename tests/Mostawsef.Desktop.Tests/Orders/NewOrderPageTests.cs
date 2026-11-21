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
    public async Task Opening_the_page_loads_the_catalog()
    {
        using var app = Admin();
        var (shell, page, window) = await ShowNewOrderAsync(app);

        var catalog = Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Equal($"{TestsPath}/all", catalog.RequestUri!.AbsolutePath);
        Assert.Equal([Cbc.Id, Sugar.Id], page.Catalog.Select(t => t.Test.Id));
        Assert.Same(page, shell.CurrentPage);
        Assert.False(page.IsPreparing);
        Assert.False(Named<LoadingOverlay>(window, "PreparingOverlay").IsLoading);
        Assert.True(Named<Card>(window, "PatientColumn").IsEffectivelyVisible);
        Assert.True(Named<Card>(window, "CatalogColumn").IsEffectivelyVisible);
        Assert.True(Named<Card>(window, "ReviewColumn").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Reopening_reuses_the_same_page_and_clears_the_previous_draft()
    {
        using var app = Admin();
        var (shell, orders, window) = await ShowOrdersPageAsync(app);
        await orders.NewOrderCommand.ExecuteAsync(null);
        var page = Assert.IsType<NewOrderViewModel>(shell.CurrentPage);
        await WaitUntil(() => !page.IsPreparing && !page.IsCatalogLoading);

        page.SelectPatientCommand.Execute(Mona);
        AddTests(page, Cbc);
        Assert.True(page.IsDirty);

        var discard = page.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await discard;
        await WaitUntil(() => shell.CurrentPage == orders);
        Assert.Empty(page.Catalog);

        await orders.NewOrderCommand.ExecuteAsync(null);
        await WaitUntil(() => !page.IsPreparing && !page.IsCatalogLoading);

        Assert.Same(page, shell.CurrentPage);
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
    public async Task A_second_order_uses_the_cached_catalog()
    {
        using var app = Admin();
        var (shell, orders, window) = await ShowOrdersPageAsync(app);
        await orders.NewOrderCommand.ExecuteAsync(null);
        var page = Assert.IsType<NewOrderViewModel>(shell.CurrentPage);
        await WaitUntil(() => !page.IsPreparing && !page.IsCatalogLoading);

        var discard = page.BackCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(window.GetVisualDescendants().OfType<ConfirmDialog>());
        await discard;
        await WaitUntil(() => shell.CurrentPage == orders);
        await orders.NewOrderCommand.ExecuteAsync(null);
        await WaitUntil(() => !page.IsPreparing && !page.IsCatalogLoading);

        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
        Assert.Equal([Cbc.Id, Sugar.Id], page.Catalog.Select(t => t.Test.Id));
        Assert.All(page.Catalog, t => Assert.False(t.IsSelected));
    }

    [AvaloniaFact]
    public async Task Catalog_reloads_after_lab_tests_are_mutated()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);
        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));

        app.Get<LabTestCatalogVersion>().Invalidate();
        await page.RetryCatalogCommand.ExecuteAsync(null);
        await WaitUntil(() => app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count() == 2);

        Assert.Equal(2, app.Api.RequestsTo(HttpMethod.Get, TestsPath).Count());
    }

    [AvaloniaFact]
    public async Task Searching_the_catalog_filters_it_in_memory_without_asking_the_server()
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
        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, TestsPath));
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
    public async Task The_total_is_the_lab_costs_plus_our_price_and_caps_the_payment_live()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        FillReadyOrder(page, Cbc, Sugar);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Cbc.LabCost + Sugar.LabCost, page.LabsTotal);
        Assert.Equal(20000, page.Total);
        Assert.Equal(200m, page.MaxPayableAmount);
        Assert.Equal("200.00", Named<TextBlock>(window, "LabsText").Text);
        Assert.Equal("200.00", Named<TextBlock>(window, "TotalText").Text);
        Assert.False(Named<TextBlock>(window, "RemainingText").IsEffectivelyVisible);

        page.Addon = 5m;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(20500, page.Total);
        Assert.Equal(205m, page.MaxPayableAmount);

        page.Addon = 200m;
        page.PaymentAmount = 50m;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(40000, page.Total);
        Assert.Equal(400m, page.MaxPayableAmount);
        Assert.Equal("400.00", Named<TextBlock>(window, "TotalText").Text);
        Assert.Equal("350.00", Named<TextBlock>(window, "RemainingText").Text);
        Assert.True(Named<TextBlock>(window, "RemainingText").IsEffectivelyVisible);
        Assert.Equal(400m, Named<NumericUpDown>(window, "PaymentAmountBox").Maximum);
    }

    [AvaloniaFact]
    public async Task Create_needs_patient_and_tests()
    {
        using var app = Admin();
        var (_, page, _) = await ShowNewOrderAsync(app);

        Assert.True(page.SaveCommand.CanExecute(null));

        await page.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ErrorCode.validation", page.FormErrorKey);
        Assert.Equal(ErrorCodes.Required, page.Errors[nameof(CreateOrderRequest.PatientId)]);
        Assert.Equal(ErrorCodes.Required, page.Errors[nameof(CreateOrderRequest.Items)]);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, OrdersPath));

        page.SelectPatientCommand.Execute(Mona);
        Assert.True(page.IsPatientSatisfied);
        AddTests(page, Cbc);
        Assert.True(page.IsTestsSatisfied);
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
        AddTests(page, Sugar);
        page.Addon = 50m;
        page.PaymentAmount = 110m;

        await page.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Invalid, page.PatientForm.Errors[nameof(PatientFormViewModel.Phone)]);
        Assert.Equal(ErrorCodes.ExceedsTotal, page.Errors[nameof(page.PaymentAmount)]);
        Assert.Equal("Some fields need attention.", Named<Alert>(window, "FormError").Text);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, OrdersPath));
    }

    [AvaloniaFact]
    public async Task A_valid_order_is_sent_with_an_addon_then_its_details_open()
    {
        using var app = Admin();
        var created = Details(Guid.NewGuid()) with { Total = 20500, Paid = 5000, Balance = 15500, Addon = 20500 };
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
        FillReadyOrder(page, Cbc, Sugar);
        page.Addon = 205m;
        page.PaymentAmount = 50m;
        page.PaymentMethod = PaymentMethods.Card;
        page.PaymentNotes = "  ";

        await page.SaveCommand.ExecuteAsync(null);
        var details = Assert.IsType<OrderDetailsViewModel>(shell.CurrentPage);
        await WaitUntil(() => details.HasOrder && Toasts(window).Count > 0);

        Assert.True(details.HasOrder);
        Assert.True(details.HasAddon);
        Assert.Equal(20500, details.Order!.Addon);
        Assert.True(Named<StackPanel>(window, "AddonRow").IsVisible);
        Assert.Equal("205.00", Named<TextBlock>(window, "AddonText").Text);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, $"{OrdersPath}/{created.Id}"));
        Assert.NotNull(sent);
        Assert.Null(sent.PatientId);
        Assert.Equal(("Omar Khaled", "0111", Genders.Male), (sent.NewPatient!.FullName, sent.NewPatient.Phone, sent.NewPatient.Gender));
        Assert.Equal(
            [new CreateOrderItemRequest { LabTestId = Cbc.Id }, new CreateOrderItemRequest { LabTestId = Sugar.Id }],
            sent.Items);
        Assert.Equal(20500, sent.Addon);
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
        FillReadyOrder(page, Cbc, Sugar);
        page.Addon = 200m;
        page.PaymentAmount = 10m;

        await page.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Invalid, page.PatientForm.Errors[nameof(PatientFormViewModel.Phone)]);
        Assert.Null(page.SelectedTests[0].Errors[nameof(CreateOrderItemRequest.LabTestId)]);
        Assert.Equal(ErrorCodes.Inactive, page.SelectedTests[1].Errors[nameof(CreateOrderItemRequest.LabTestId)]);
        Assert.Equal(ErrorCodes.ExceedsTotal, page.Errors[nameof(page.PaymentAmount)]);
        Assert.Equal("ErrorCode.validation", page.FormErrorKey);
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

        FillReadyOrder(page, Cbc);

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
    [InlineData(AppLanguage.English, "Patient", "Selected tests (0)", "Lab tests", "Charge and payment", "Create order")]
    [InlineData(AppLanguage.Arabic, "المريض", "التحاليل المختارة (0)", "التحاليل", "السعر والدفع", "إنشاء الطلب")]
    public async Task The_single_page_sections_render_in_both_languages(
        AppLanguage language, string patient, string selected, string catalog, string charge, string create)
    {
        using var app = Admin(language);
        var (_, _, window) = await ShowNewOrderAsync(app);
        Dispatcher.UIThread.RunJobs();

        var texts = Texts(window);
        Assert.Contains(patient, texts);
        Assert.Contains(selected, texts);
        Assert.Contains(catalog, texts);
        Assert.Contains(charge, texts);
        Assert.Contains(create, texts);
        Assert.True(Named<Card>(window, "PatientColumn").IsEffectivelyVisible);
        Assert.True(Named<Card>(window, "TestsColumn").IsEffectivelyVisible);
        Assert.True(Named<Card>(window, "ReviewColumn").IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "CreateButton").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task At_the_minimum_window_size_all_sections_stay_reachable()
    {
        using var app = Admin();
        var (_, _, window) = await ShowNewOrderAsync(app, width: 720, height: 520);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Named<Card>(window, "PatientColumn").IsEffectivelyVisible);
        Assert.True(Named<Button>(window, "CreateButton").IsEffectivelyVisible);
        Assert.True(Named<Card>(window, "ReviewColumn").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task The_test_lists_are_capped_so_a_long_catalog_does_not_push_charge_away()
    {
        using var app = Admin();
        var (_, _, window) = await ShowNewOrderAsync(app);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(360, Named<ItemsControl>(window, "CatalogList").MaxHeight);
        Assert.Equal(360, Named<ItemsControl>(window, "SelectedTestsList").MaxHeight);
    }

    [AvaloniaFact]
    public async Task Our_price_has_its_own_row_above_the_first_payment_fields()
    {
        using var app = Admin();
        var (_, page, window) = await ShowNewOrderAsync(app);
        FillReadyOrder(page, Cbc);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var addon = Named<NumericUpDown>(window, "AddonBox");
        var amount = Named<NumericUpDown>(window, "PaymentAmountBox");
        var method = Named<ComboBox>(window, "PaymentMethodBox");

        Assert.Null(addon.FindAncestorOfType<ResponsiveGrid>());
        Assert.Same(amount.FindAncestorOfType<ResponsiveGrid>(), method.FindAncestorOfType<ResponsiveGrid>());
        var addonY = addon.TranslatePoint(default, window)!.Value.Y;
        var amountY = amount.TranslatePoint(default, window)!.Value.Y;
        Assert.True(addonY + addon.Bounds.Height <= amountY, $"our price at {addonY}, first payment at {amountY}");
    }

    private static void FillReadyOrder(NewOrderViewModel page, params Contracts.LabTests.LabTestDto[] tests)
    {
        if (!page.HasPatient)
        {
            page.SelectPatientCommand.Execute(Mona);
        }

        AddTests(page, tests);
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

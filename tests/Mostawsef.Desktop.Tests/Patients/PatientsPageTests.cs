using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.Patients;

[Collection(UiTestCollection.Name)]
public class PatientsPageTests : UiTestBase
{
    private const string PatientsPath = "/api/patients";

    private static readonly PatientDto Mona = new(
        Guid.NewGuid(), "Mona Adel", "010 1234 5678", "012 0000 0000", "Allergic to penicillin", true, Genders.Female,
        new DateOnly(2015, 3, 9), DateTime.UtcNow, null, null, 4);

    private static readonly PatientDto Omar = new(
        Guid.NewGuid(), "عمر خالد", "011 2222 3333", null, null, false, Genders.Male, null, DateTime.UtcNow, null, null, 0);

    [AvaloniaFact]
    public void Only_admins_can_open_the_patients_page()
    {
        using var app = new TestServices();
        Assert.False(app.Navigation.CanAccess(typeof(PatientsViewModel)));

        app.SignInAs(Roles.Super);
        Assert.False(app.Navigation.CanAccess(typeof(PatientsViewModel)));

        app.SignInAs(Roles.Admin);
        Assert.True(app.Navigation.CanAccess(typeof(PatientsViewModel)));
    }

    [AvaloniaFact]
    public async Task The_table_lists_each_patient_with_phones_gender_insurance_and_row_actions()
    {
        using var app = AdminWithPatients(Mona, Omar);

        var (page, window) = await ShowPage(app);

        Assert.False(page.IsGridView);
        Assert.Empty(VisibleCards(window));
        var texts = Texts(window);
        Assert.Contains("Mona Adel", texts);
        Assert.Contains("010 1234 5678", texts);
        Assert.Contains("012 0000 0000", texts);
        Assert.Contains("Female", texts);
        Assert.Contains("Male", texts);
        Assert.Contains(new DateOnly(2015, 3, 9).ToString("d", app.Localization.Culture), texts);
        Assert.Contains("2 patients", texts);
        var badges = window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text).ToList();
        Assert.Contains("Insured", badges);
        Assert.Contains("Not insured", badges);
        Assert.Equal(2, RowButtons(Table(window)).Count(b => b.Command == page.EditCommand));
        Assert.Equal(2, RowButtons(Table(window)).Count(b => b.Command == page.DeleteCommand));
    }

    [AvaloniaFact]
    public async Task Each_row_has_its_number_a_colored_first_letter_and_copy_buttons_for_name_and_phone()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(PatientsPath, new PagedResult<PatientDto>([Mona, Omar], 2, 40, 42));

        var (page, window) = await ShowPage(app);

        Assert.Equal(41, page.FirstRowNumber);
        var numbers = window.GetVisualDescendants().OfType<RowNumber>().ToDictionary(n => ((PatientDto)n.DataContext!).Id, n => n.Text);
        Assert.Equal(("41", "42"), (numbers[Mona.Id], numbers[Omar.Id]));
        var avatars = Table(window).GetVisualDescendants().OfType<Avatar>().ToList();
        Assert.Equal(["M", "ع"], avatars.Select(a => a.Initials).Order());
        Assert.All(avatars, a => Assert.True(a.IsColorful));
        var copies = Table(window).GetVisualDescendants().OfType<CopyButton>().Select(b => b.Text).ToList();
        Assert.Contains(Mona.FullName, copies);
        Assert.Contains(Mona.Phone, copies);
        Assert.Contains(Omar.Phone, copies);
    }

    [AvaloniaFact]
    public async Task The_grid_shows_a_card_per_patient_with_edit_and_delete()
    {
        using var app = AdminWithPatients(Mona, Omar);
        var (page, window) = await ShowPage(app);

        page.ShowGridCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Table(window).IsEffectivelyVisible);
        var cards = VisibleCards(window).ToList();
        Assert.Equal<string?>([Mona.FullName, Omar.FullName], cards.Select(c => c.Title));
        Assert.All(cards, card =>
            Assert.Equal<object?>([page.EditCommand, page.DeleteCommand], RowButtons(card).Select(b => b.Command)));
        var monaTexts = cards[0].GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
        Assert.Contains("010 1234 5678", monaTexts);
        Assert.Contains("Female", monaTexts);
        Assert.Contains("Insured", monaTexts);
        Assert.DoesNotContain("Allergic to penicillin", monaTexts);
        Assert.Contains("selected", Named<Button>(window, "GridViewButton").Classes);

        page.ShowTableCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(Table(window).IsEffectivelyVisible);
        Assert.Empty(VisibleCards(window));
    }

    [AvaloniaFact]
    public async Task Edit_on_a_card_opens_the_form_filled_in_and_sends_the_loaded_version()
    {
        using var app = AdminWithPatients(Mona);
        UpdatePatientRequest? sent = null;
        app.Api.On(HttpMethod.Put, $"{PatientsPath}/{Mona.Id}", request =>
        {
            sent = request.Content!.ReadFromJsonAsync<UpdatePatientRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(Mona with { Version = 5 });
        });
        var (page, window) = await ShowPage(app);
        page.ShowGridCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var edit = RowButtons(VisibleCards(window).Single()).First();
        edit.Command!.Execute(edit.CommandParameter);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm(window);
        Assert.Equal(
            (Mona.FullName, Mona.Phone, Mona.GuardianPhone, Mona.Notes, true, Genders.Female, Mona.DateOfBirth),
            (form.FullName, form.Phone, form.GuardianPhone, form.Notes, form.IsInsured, form.Gender, form.DateOfBirth));
        Assert.Equal("Edit patient", window.GetVisualDescendants().OfType<Modal>().Single().Title);

        form.GuardianPhone = "  ";
        form.IsInsured = false;
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new UpdatePatientRequest
        {
            FullName = Mona.FullName, Phone = Mona.Phone, GuardianPhone = null, Notes = Mona.Notes, IsInsured = false,
            Gender = Genders.Female, DateOfBirth = Mona.DateOfBirth, Version = Mona.Version,
        }, sent);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, $"{PatientsPath}/{Mona.Id}"));
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.Contains("Patient saved.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Saving_an_empty_new_patient_shows_field_errors_without_calling_the_api()
    {
        using var app = AdminWithPatients(Mona);
        var (page, window) = await ShowPage(app);
        var form = OpenAddForm(page, window);

        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ErrorCodes.Required, form.Errors[nameof(form.FullName)]);
        Assert.Equal(ErrorCodes.Required, form.Errors[nameof(form.Phone)]);
        Assert.Equal("This field is required.", FieldError(window, "Gender"));
        Assert.Null(form.Errors[nameof(form.DateOfBirth)]);
        app.Localization.SetLanguage(AppLanguage.Arabic);
        Assert.Equal("هذا الحقل مطلوب.", FieldError(window, "النوع"));
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, PatientsPath));
    }

    [AvaloniaFact]
    public async Task Values_past_the_shared_limits_are_refused_before_calling_the_api()
    {
        using var app = AdminWithPatients();
        var form = new PatientFormViewModel(app.Get<PatientsApi>(), app.Get<IDialogService>(), app.Localization)
        {
            FullName = new string('n', PatientRules.FullNameMaxLength + 1),
            Phone = "call me",
            GuardianPhone = new string('1', PatientRules.PhoneMaxLength + 1),
            Notes = new string('x', PatientRules.NotesMaxLength + 1),
            Gender = Genders.Male,
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddDays(1),
        };

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal($"max_length|{PatientRules.FullNameMaxLength}", form.Errors[nameof(form.FullName)]);
        Assert.Equal(ErrorCodes.Invalid, form.Errors[nameof(form.Phone)]);
        Assert.Equal($"max_length|{PatientRules.PhoneMaxLength}", form.Errors[nameof(form.GuardianPhone)]);
        Assert.Equal($"max_length|{PatientRules.NotesMaxLength}", form.Errors[nameof(form.Notes)]);
        Assert.Equal(ErrorCodes.Invalid, form.Errors[nameof(form.DateOfBirth)]);

        form.DateOfBirth = new DateOnly(1899, 12, 31);
        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal(ErrorCodes.Invalid, form.Errors[nameof(form.DateOfBirth)]);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Post, PatientsPath));
    }

    [AvaloniaFact]
    public async Task A_new_patient_is_sent_trimmed_with_blank_optional_fields_as_null_then_the_list_inserts()
    {
        using var app = AdminWithPatients(Mona);
        CreatePatientRequest? sent = null;
        app.Api.On(HttpMethod.Post, PatientsPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<CreatePatientRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(Omar);
        });
        var (page, window) = await ShowPage(app);
        var listRequests = app.Api.RequestsTo(HttpMethod.Get, PatientsPath).Count();

        var add = page.AddCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = CurrentForm(window);
        form.FullName = "  Omar Khaled ";
        form.Phone = " 011 2222 3333 ";
        form.Notes = "   ";
        form.Gender = Genders.Male;
        form.DateOfBirth = new DateOnly(1990, 5, 17);
        await form.SaveCommand.ExecuteAsync(null);
        await add;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new CreatePatientRequest
        {
            FullName = "Omar Khaled", Phone = "011 2222 3333", GuardianPhone = null, Notes = null, IsInsured = false,
            Gender = Genders.Male, DateOfBirth = new DateOnly(1990, 5, 17),
        }, sent);
        Assert.Equal(listRequests, app.Api.RequestsTo(HttpMethod.Get, PatientsPath).Count());
        Assert.Contains(page.Items, p => p.Id == Omar.Id);
        Assert.Contains("Patient saved.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Form_inputs_have_placeholders_and_the_same_limits_as_the_api()
    {
        using var app = AdminWithPatients();
        var (page, window) = await ShowPage(app);
        OpenAddForm(page, window);

        Assert.Equal(("e.g. Mona Adel Hassan", PatientRules.FullNameMaxLength), Box(window, "FullNameBox"));
        Assert.Equal(("e.g. 010 1234 5678", PatientRules.PhoneMaxLength), Box(window, "PhoneBox"));
        Assert.Equal(("Optional, e.g. a parent's number", PatientRules.PhoneMaxLength), Box(window, "GuardianPhoneBox"));
        Assert.Equal(PatientRules.NotesMaxLength, Named<TextBox>(window, "NotesBox").MaxLength);
        Assert.Equal("Choose...", Named<ComboBox>(window, "GenderBox").PlaceholderText);
        var date = Named<CalendarDatePicker>(window, "DateOfBirthBox");
        Assert.Equal("Optional", date.PlaceholderText);
        Assert.Equal(new DateTime(1900, 1, 1), date.DisplayDateStart);
        Assert.Equal(DateTime.Today, date.DisplayDateEnd);
    }

    [AvaloniaTheory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Arabic)]
    public async Task The_search_box_is_centered_and_the_list_asks_for_40_patients_per_page(AppLanguage language)
    {
        using var app = AdminWithPatients(Mona);
        app.Localization.SetLanguage(language);
        var (page, window) = await ShowPage(app);

        var search = Named<SearchBox>(window, "SearchBox");
        var toolbar = (Grid)search.Parent!;
        var start = search.TranslatePoint(default, toolbar)!.Value.X;
        var end = toolbar.Bounds.Width - start - search.Bounds.Width;

        Assert.InRange(start - end, -1, 1);
        Assert.True(search.Bounds.Width >= toolbar.Bounds.Width / 2 - 10, $"{search.Bounds.Width} of {toolbar.Bounds.Width}");
        Assert.Equal(PageQuery.MaxSearchLength, search.MaxLength);
        Assert.Equal(app.Localization.Get("Patients.SearchPlaceholder"), search.PlaceholderText);
        Assert.Equal(40, page.PageSize);
        Assert.Contains("pageSize=40", app.Api.RequestsTo(HttpMethod.Get, PatientsPath).Single().RequestUri!.Query);
    }

    [AvaloniaFact]
    public async Task Delete_asks_first_then_deletes_and_reloads()
    {
        using var app = AdminWithPatients(Mona);
        app.Api.On(HttpMethod.Delete, $"{PatientsPath}/{Mona.Id}", HttpStatusCode.NoContent);
        var (page, window) = await ShowPage(app);
        var listRequests = app.Api.RequestsTo(HttpMethod.Get, PatientsPath).Count();

        var cancelled = page.DeleteCommand.ExecuteAsync(Mona);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Texts(window), t => t == "Delete the patient \"Mona Adel\"?");
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(false);
        await cancelled;
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Delete, PatientsPath));

        var delete = page.DeleteCommand.ExecuteAsync(Mona);
        Dispatcher.UIThread.RunJobs();
        window.GetVisualDescendants().OfType<ConfirmDialog>().Single().Close(true);
        await delete;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.Api.RequestsTo(HttpMethod.Delete, $"{PatientsPath}/{Mona.Id}"));
        Assert.True(app.Api.RequestsTo(HttpMethod.Get, PatientsPath).Count() > listRequests);
        Assert.Contains("Patient deleted.", Toasts(window));
    }

    [AvaloniaFact]
    public async Task Export_reads_every_patient_whatever_the_search_and_opens_a_pdf()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var everyone = Enumerable.Range(1, 3)
            .Select(i => Omar with { Id = Guid.NewGuid(), FullName = $"Patient {i:000}" })
            .ToList();
        app.Api.OnGet(PatientsPath, new PagedResult<PatientDto>([Omar], 1, 40, 1));
        app.Api.OnGet($"{PatientsPath}/all", (IReadOnlyList<PatientDto>)everyone);
        var (page, _) = await ShowPage(app);
        page.Search = "Patient 001";

        await page.ExportCommand.ExecuteAsync(null);

        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, $"{PatientsPath}/all"));
        var file = Assert.Single(app.OpenedFiles);
        Assert.StartsWith("patients-", Path.GetFileName(file));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(file), 0, 4));
        File.Delete(file);
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
        using var app = AdminWithPatients(Mona, Omar);
        var (page, window) = await ShowPage(app);

        app.Localization.SetLanguage(AppLanguage.Arabic);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, window.FlowDirection);
        var texts = Texts(window);
        Assert.Contains("المرضى", texts);
        Assert.Contains("أنثى", texts);
        Assert.Contains("غير مؤمَّن", window.GetVisualDescendants().OfType<Badge>().Where(b => b.IsEffectivelyVisible).Select(b => b.Text));

        page.ShowGridCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, VisibleCards(window).Count());
        Assert.Contains("ذكر", Texts(window));
    }

    [AvaloniaFact]
    public async Task The_form_view_is_reused_and_always_shows_the_current_patient_unchanged()
    {
        using var app = AdminWithPatients(Mona, Omar);
        var (page, window) = await ShowPage(app);

        async Task<(PatientFormViewModel Form, Control View)> OpenAsync(Func<Task> open)
        {
            _ = open();
            Dispatcher.UIThread.RunJobs();
            var form = CurrentForm(window);
            var view = Named<TextBox>(window, "FullNameBox").FindAncestorOfType<UserControl>()!;
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            return (form, view);
        }

        async Task CancelAsync(PatientFormViewModel form)
        {
            await form.CancelCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        }

        var (monaForm, firstView) = await OpenAsync(() => page.EditCommand.ExecuteAsync(Mona));
        Assert.False(monaForm.IsDirty);
        Assert.Equal(Genders.Female, monaForm.Gender);
        await CancelAsync(monaForm);

        var (omarForm, secondView) = await OpenAsync(() => page.EditCommand.ExecuteAsync(Omar));
        Assert.Same(firstView, secondView);
        Assert.False(omarForm.IsDirty);
        Assert.Equal((Omar.FullName, Omar.Phone, Genders.Male, (DateOnly?)null, false),
            (omarForm.FullName, omarForm.Phone, omarForm.Gender, omarForm.DateOfBirth, omarForm.IsInsured));
        Assert.Equal(Omar.FullName, Named<TextBox>(window, "FullNameBox").Text);
        Assert.Equal(Genders.Male, Named<ComboBox>(window, "GenderBox").SelectedItem);
        Assert.Null(Named<CalendarDatePicker>(window, "DateOfBirthBox").SelectedDate);
        Assert.False(Named<CheckBox>(window, "IsInsuredBox").IsChecked);
        Assert.Equal(Genders.Female, monaForm.Gender);
        await CancelAsync(omarForm);

        var (newForm, thirdView) = await OpenAsync(() => page.AddCommand.ExecuteAsync(null));
        Assert.Same(firstView, thirdView);
        Assert.False(newForm.IsDirty);
        Assert.Equal(string.Empty, Named<TextBox>(window, "FullNameBox").Text ?? string.Empty);
        Assert.Null(Named<ComboBox>(window, "GenderBox").SelectedItem);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Put, PatientsPath));
    }

    private static TestServices AdminWithPatients(params PatientDto[] patients)
    {
        var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(PatientsPath, new PagedResult<PatientDto>(patients, 1, 40, patients.Length));
        return app;
    }

    /// <summary>The page inside a DialogHost (attached to the DialogService) with toasts attached to the window.</summary>
    private static async Task<(PatientsViewModel Page, Window Window)> ShowPage(TestServices app)
    {
        var page = app.Get<PatientsViewModel>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        app.Get<ToastService>().Attach(window);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }

    private static PatientFormViewModel OpenAddForm(PatientsViewModel page, Window window)
    {
        _ = page.AddCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        return CurrentForm(window);
    }

    private static PatientFormViewModel CurrentForm(Window window) =>
        (PatientFormViewModel)window.GetVisualDescendants().OfType<Modal>().Last().Content!;

    private static DataGrid Table(Window window) => window.GetVisualDescendants().OfType<DataGrid>().Single();

    private static IEnumerable<Card> VisibleCards(Window window) =>
        window.GetVisualDescendants().OfType<Card>().Where(c => c.IsEffectivelyVisible);

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static (string? Placeholder, int MaxLength) Box(Window window, string name)
    {
        var box = Named<TextBox>(window, name);
        return (box.PlaceholderText, box.MaxLength);
    }

    private static IEnumerable<IconButton> RowButtons(Visual container) =>
        container.GetVisualDescendants().OfType<IconButton>().Where(b => b.CommandParameter is PatientDto);

    private static string? FieldError(Window window, string label) =>
        window.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == label && f.IsEffectivelyVisible).Error;

    private static List<string?> Toasts(Window window) =>
        window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message).ToList();
}

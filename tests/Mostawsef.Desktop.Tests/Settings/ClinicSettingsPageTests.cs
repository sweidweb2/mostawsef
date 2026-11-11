using System.Net;
using System.Net.Http.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;

namespace Mostawsef.Desktop.Tests.Settings;

[Collection(UiTestCollection.Name)]
public class ClinicSettingsPageTests : UiTestBase
{
    private const string SettingsPath = "/api/clinic-settings";

    private static readonly ClinicSettingsDto Current =
        new("عيادة النور", "12 Nile St, Cairo", "0223456789", Currencies.Egp, 4, DateTime.UtcNow, "admin@test.local");

    [AvaloniaFact]
    public void Only_admins_can_open_the_clinic_settings_page()
    {
        using var app = new TestServices();
        Assert.False(app.Navigation.CanAccess(typeof(ClinicSettingsViewModel)));

        app.SignInAs(Roles.Super);
        Assert.False(app.Navigation.CanAccess(typeof(ClinicSettingsViewModel)));

        app.SignInAs(Roles.Admin);
        Assert.True(app.Navigation.CanAccess(typeof(ClinicSettingsViewModel)));
    }

    [AvaloniaFact]
    public async Task The_page_shows_the_clinic_details_with_the_translated_currency()
    {
        using var app = AdminWithSettings();
        var (_, window) = await ShowPage(app);

        var values = Details(window);
        Assert.Equal("عيادة النور", values["Clinic name"]);
        Assert.Equal("0223456789", values["Phone"]);
        Assert.Equal("EGP", values["Currency"]);

        app.Localization.SetLanguage(AppLanguage.Arabic);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ج.م", Details(window)["العملة"]);
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
    public async Task Editing_sends_the_loaded_version_and_updates_the_whole_app()
    {
        using var app = AdminWithSettings();
        UpdateClinicSettingsRequest? sent = null;
        app.Api.On(HttpMethod.Put, SettingsPath, request =>
        {
            sent = request.Content!.ReadFromJsonAsync<UpdateClinicSettingsRequest>().GetAwaiter().GetResult();
            return FakeApiHandler.Json(Current with { Name = sent!.Name, CurrencyCode = sent.CurrencyCode, Version = 5 });
        });
        var (page, window) = await ShowPage(app);

        var edit = page.EditCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = (ClinicSettingsFormViewModel)window.GetVisualDescendants().OfType<Modal>().Single().Content!;
        form.Name = "  Al Nour Clinic ";
        form.CurrencyCode = Currencies.Usd;
        await form.SaveCommand.ExecuteAsync(null);
        await edit;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Current.Version, sent!.Version);
        Assert.Equal("Al Nour Clinic", sent.Name);
        Assert.Equal(Currencies.Usd, app.Get<ClinicContext>().CurrencyCode);
        Assert.Equal("Al Nour Clinic", Details(window)["Clinic name"]);
        Assert.Contains("Clinic settings saved.", window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message));
    }

    [AvaloniaFact]
    public async Task The_edit_form_shows_translated_placeholders()
    {
        using var app = AdminWithSettings();
        var (page, window) = await ShowPage(app);

        _ = page.EditCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        string? Placeholder(string name) =>
            window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == name).PlaceholderText;

        Assert.Equal("Enter the clinic name", Placeholder("NameBox"));
        Assert.Equal("Enter the clinic address", Placeholder("AddressBox"));
        Assert.Equal("Enter the clinic phone number", Placeholder("PhoneBox"));

        app.Localization.SetLanguage(AppLanguage.Arabic);

        Assert.Equal("أدخل اسم العيادة", Placeholder("NameBox"));
        Assert.Equal("أدخل عنوان العيادة", Placeholder("AddressBox"));
        Assert.Equal("أدخل رقم هاتف العيادة", Placeholder("PhoneBox"));
    }

    [AvaloniaFact]
    public async Task A_conflicting_edit_shows_the_conflict_message()
    {
        using var app = AdminWithSettings();
        app.Api.On(HttpMethod.Put, SettingsPath, _ => FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict));
        var (page, window) = await ShowPage(app);

        _ = page.EditCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        var form = (ClinicSettingsFormViewModel)window.GetVisualDescendants().OfType<Modal>().Single().Content!;
        form.Phone = "0100000000";
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        var alert = window.GetVisualDescendants().OfType<Alert>().Single(a => a.IsVisible);
        Assert.StartsWith("Someone else changed this record", alert.Text);
        Assert.Equal(Currencies.Egp, app.Get<ClinicContext>().CurrencyCode);
    }

    private static TestServices AdminWithSettings()
    {
        var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(SettingsPath, Current);
        return app;
    }

    private static async Task<(ClinicSettingsViewModel Page, Window Window)> ShowPage(TestServices app)
    {
        var page = app.Get<ClinicSettingsViewModel>();
        await page.ActivateAsync();
        var host = new DialogHost { Content = new ContentControl { Content = page } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        app.Get<ToastService>().Attach(window);
        Dispatcher.UIThread.RunJobs();
        return (page, window);
    }

    private static Dictionary<string, string> Details(Window window) =>
        window.GetVisualDescendants().OfType<DetailsItem>().ToDictionary(i => i.Label!, i => i.DisplayValue);
}

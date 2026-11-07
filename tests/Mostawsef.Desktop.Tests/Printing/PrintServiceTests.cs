using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Printing;

/// <summary>Arabic letter shaping can't be asserted here: open a printed test page once to check it by eye.</summary>
public class PrintServiceTests
{
    [AvaloniaTheory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Arabic)]
    public void The_test_page_renders_a_pdf_in_both_languages(AppLanguage language)
    {
        using var app = new TestServices(language);
        app.Get<ClinicContext>().Update(new ClinicSettingsDto("عيادة النور / Al Nour", "12 Nile St", "0223456789", Currencies.Egp, 0, null, null));
        var print = app.Get<PrintService>();

        var pdf = print.Render(new TestPageDocument(print.CreateContext()));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 5_000, $"Only {pdf.Length} bytes");
    }

    [AvaloniaFact]
    public void A_page_prints_even_before_the_clinic_details_are_loaded()
    {
        using var app = new TestServices();
        var print = app.Get<PrintService>();

        var pdf = print.Render(new TestPageDocument(print.CreateContext()));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [AvaloniaFact]
    public async Task Printing_saves_the_file_in_the_print_folder_and_opens_it()
    {
        using var app = new TestServices();
        var print = app.Get<PrintService>();

        var path = await print.PrintAsync(new TestPageDocument(print.CreateContext()), "unit-test");

        Assert.Equal(path, Assert.Single(app.OpenedFiles));
        Assert.Equal(PrintService.PrintFolder, Path.GetDirectoryName(path));
        Assert.StartsWith("unit-test-", Path.GetFileName(path));
        Assert.True(File.Exists(path));
        File.Delete(path);
    }
}

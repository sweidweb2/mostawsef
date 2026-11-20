using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Patients;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Printing;

/// <summary>Arabic letter shaping can't be asserted here: open an exported lab test list once to check it by eye.</summary>
[Collection(UiTestCollection.Name)]
public class PrintServiceTests : UiTestBase
{
    private static readonly LabTestDto[] Tests =
    [
        new(Guid.NewGuid(), "CBC", "تحليل صورة دم كاملة / Complete blood count", 18050, true, DateTime.UtcNow, null, null, 0),
        new(Guid.NewGuid(), "FBS", "Fasting blood sugar", 4000, false, DateTime.UtcNow, null, null, 0),
    ];

    [AvaloniaTheory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Arabic)]
    public void The_lab_test_list_renders_a_pdf_in_both_languages(AppLanguage language)
    {
        using var app = new TestServices(language);
        app.Get<ClinicContext>().Update(new ClinicSettingsDto("عيادة النور / Al Nour", "12 Nile St", "0223456789", Currencies.Egp, 0, null, null));
        var print = app.Get<PrintService>();

        var pdf = print.Render(new LabTestsDocument(print.CreateContext(), Tests));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > 5_000, $"Only {pdf.Length} bytes");
    }

    [AvaloniaTheory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Arabic)]
    public void The_patient_list_renders_a_pdf_in_both_languages(AppLanguage language)
    {
        using var app = new TestServices(language);
        var print = app.Get<PrintService>();
        PatientDto[] patients =
        [
            new(Guid.NewGuid(), "منى عادل / Mona Adel", "010 1234 5678", "012 0000 0000", "Private note", true, Genders.Female,
                new DateOnly(2015, 3, 9), DateTime.UtcNow, null, null, 0),
            new(Guid.NewGuid(), "Omar Khaled", "011 2222 3333", null, null, false, Genders.Male, null, DateTime.UtcNow, null, null, 0),
        ];

        var pdf = print.Render(new PatientsDocument(print.CreateContext(), patients));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [AvaloniaFact]
    public void A_document_prints_even_before_the_clinic_details_are_loaded_and_with_no_rows()
    {
        using var app = new TestServices();
        var print = app.Get<PrintService>();

        var pdf = print.Render(new LabTestsDocument(print.CreateContext(), []));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [AvaloniaFact]
    public async Task Printing_saves_the_file_in_the_print_folder_and_opens_it()
    {
        using var app = new TestServices();
        var print = app.Get<PrintService>();

        var path = await print.PrintAsync(new LabTestsDocument(print.CreateContext(), Tests), "unit-test");

        Assert.Equal(path, Assert.Single(app.OpenedFiles));
        Assert.Equal(PrintService.PrintFolder, Path.GetDirectoryName(path));
        Assert.StartsWith("unit-test-", Path.GetFileName(path));
        Assert.True(File.Exists(path));
        File.Delete(path);
    }

    [AvaloniaFact]
    public async Task Printing_deletes_pdfs_older_than_a_day_from_the_print_folder()
    {
        using var app = new TestServices();
        Directory.CreateDirectory(PrintService.PrintFolder);
        var stale = Path.Combine(PrintService.PrintFolder, "stale-old.pdf");
        await File.WriteAllBytesAsync(stale, "%PDF-stale"u8.ToArray(), TestContext.Current.CancellationToken);
        File.SetLastWriteTime(stale, DateTime.Now.AddDays(-2));
        var print = app.Get<PrintService>();

        var path = await print.PrintAsync(new LabTestsDocument(print.CreateContext(), Tests), "cleanup");

        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(path));
        File.Delete(path);
    }
}

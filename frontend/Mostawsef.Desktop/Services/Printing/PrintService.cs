using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Mostawsef.Desktop.Services.Printing;

/// <summary>
/// Turns QuestPDF documents into PDF files and opens them in the default viewer, where the user prints.
/// Documents use <see cref="ClinicPage"/> for the shared header/footer and the app's Arabic + Latin font.
/// </summary>
public class PrintService(
    LocalizationService localization,
    ClinicContext clinic,
    Action<string> openFile,
    PerformanceMonitor perf)
{
    public const string FontFamily = "IBM Plex Sans Arabic";

    private static readonly string[] FontFiles =
        ["IBMPlexSansArabic-Regular.ttf", "IBMPlexSansArabic-Medium.ttf", "IBMPlexSansArabic-SemiBold.ttf", "IBMPlexSansArabic-Bold.ttf"];

    private static readonly Lazy<bool> Setup = new(() =>
    {
        // Free for organizations under USD 1M yearly revenue: https://www.questpdf.com/license/
        QuestPDF.Settings.License = LicenseType.Community;
        foreach (var file in FontFiles)
        {
            using var font = AssetLoader.Open(new Uri($"avares://Mostawsef.Desktop/Assets/Fonts/{file}"));
            QuestPDF.Drawing.FontManager.RegisterFontFromStream(font);
        }

        return true;
    });

    /// <summary>
    /// The first PDF of a session is slow: QuestPDF loads its native renderer and the fonts. This does that once per
    /// process on a background thread (a one-line PDF that is thrown away), so the user's first print is quick.
    /// A failure is only traced: the real print hits the same problem and reports it.
    /// </summary>
    private static readonly Lazy<Task> Warm = new(() => Task.Run(() =>
    {
        try
        {
            _ = Setup.Value;
            Document.Create(c => c.Page(page =>
                    page.Content().Text("م m").FontFamily(FontFamily)))
                .GeneratePdf();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("PDF warm-up failed: {0}", ex);
        }
    }));

    /// <summary>Starts <see cref="Warm"/> (once per process). Call it where printing becomes possible (Admin portal).</summary>
    public static void WarmUpInBackground() => _ = Warm.Value;

    public static string PrintFolder { get; } = Path.Combine(Path.GetTempPath(), "Mostawsef", "print");

    /// <summary>Opens a file with the program Windows associates with it (the PDF viewer).</summary>
    public static void OpenWithDefaultApp(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });

    /// <summary>What every document needs: clinic details, language and the print time. Take it on the UI thread.</summary>
    public PrintContext CreateContext() => new(clinic.Settings, localization, DateTime.Now);

    /// <summary>Loads clinic settings if needed, then builds the print context. Prefer this before exports.</summary>
    public async Task<PrintContext> CreateContextAsync(CancellationToken cancellationToken = default)
    {
        await clinic.LoadAsync(cancellationToken: cancellationToken);
        return CreateContext();
    }

    public byte[] Render(IDocument document)
    {
        _ = Setup.Value;
        return document.GeneratePdf();
    }

    /// <summary>Renders, cleans old PDFs and writes the file off the UI thread, then opens it. Returns its path.</summary>
    public async Task<string> PrintAsync(IDocument document, string name)
    {
        using var scope = perf.Measure("Print", name);
        var path = await Task.Run(() =>
        {
            var pdf = Render(document);
            Directory.CreateDirectory(PrintFolder);
            DeleteOldPrints();

            var file = Path.Combine(PrintFolder, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.pdf");
            File.WriteAllBytes(file, pdf);
            return file;
        });

        openFile(path);
        return path;
    }

    private static void DeleteOldPrints()
    {
        foreach (var old in new DirectoryInfo(PrintFolder).GetFiles("*.pdf").Where(f => f.LastWriteTime < DateTime.Now.AddDays(-1)))
        {
            try
            {
                old.Delete();
            }
            catch (IOException)
            {
                // Still open in a viewer: try again next time.
            }
        }
    }
}

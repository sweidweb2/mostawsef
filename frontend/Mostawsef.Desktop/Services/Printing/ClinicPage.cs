using System;
using System.Linq;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Services.Local;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Mostawsef.Desktop.Services.Printing;

/// <summary>Everything a printout needs besides its own data. Built by PrintService.CreateContext.</summary>
public sealed record PrintContext(ClinicSettingsDto? Clinic, LocalizationService Localization, DateTime PrintedAt)
{
    public string Text(string key) => Localization.Get(key);
}

/// <summary>
/// The shared A4 page of every printout (prescriptions, invoices, reports): clinic header, the document's content,
/// and a footer with the page number and print time. Right-to-left when the app is in Arabic.
/// </summary>
public static class ClinicPage
{
    public static void Compose(IDocumentContainer container, PrintContext context, Action<IContainer> content) =>
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontFamily(PrintService.FontFamily).FontSize(10).FontColor(Colors.Grey.Darken4));
            if (context.Localization.IsRightToLeft)
            {
                page.ContentFromRightToLeft();
            }

            page.Header().Element(header => Header(header, context));
            page.Content().PaddingVertical(12).Element(body => content(body));
            page.Footer().Element(footer => Footer(footer, context));
        });

    private static void Header(IContainer container, PrintContext context) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(8).Column(column =>
        {
            column.Spacing(2);
            column.Item().Text(context.Clinic?.Name ?? context.Text("App.Title")).FontSize(16).SemiBold();

            var contact = string.Join("  |  ", new[] { context.Clinic?.Address, context.Clinic?.Phone }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (contact.Length > 0)
            {
                column.Item().Text(contact).FontColor(Colors.Grey.Darken1);
            }
        });

    private static void Footer(IContainer container, PrintContext context) =>
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(text =>
            {
                text.Span(context.Text("Print.Page") + " ");
                text.CurrentPageNumber();
                text.Span(" " + context.Text("Print.Of") + " ");
                text.TotalPages();
            });
            row.RelativeItem().AlignRight().Text(
                $"{context.Text("Print.PrintedAt")} {context.PrintedAt.ToString("g", context.Localization.Culture)}")
                .FontColor(Colors.Grey.Darken1);
        });
}

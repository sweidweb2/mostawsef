using System.Collections.Generic;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.Converters;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Mostawsef.Desktop.Services.Printing;

/// <summary>The whole lab test catalog: code, name, lab cost and status, in the order given.</summary>
public sealed class LabTestsDocument(PrintContext context, IReadOnlyList<LabTestDto> tests) : IDocument
{
    private static readonly string[] Columns =
        ["LabTests.Col.Code", "LabTests.Col.Name", "LabTests.Col.LabCost", "LabTests.Col.Status"];

    public void Compose(IDocumentContainer container) => ClinicPage.Compose(container, context, content => content.Column(column =>
    {
        column.Spacing(10);
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(context.Text("Nav.LabTests")).FontSize(14).SemiBold();
            row.AutoItem().AlignBottom().Text(context.Localization.Format("LabTests.Records", tests.Count)).FontColor(Colors.Grey.Darken1);
        });

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(5);
                columns.RelativeColumn(2.5f);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                foreach (var key in Columns)
                {
                    header.Cell().Element(HeaderCell).Text(context.Text(key));
                }
            });

            foreach (var test in tests)
            {
                table.Cell().Element(BodyCell).Text(test.Code);
                table.Cell().Element(BodyCell).Text(test.Name);
                table.Cell().Element(BodyCell).Text(Money(test.LabCost));
                table.Cell().Element(BodyCell).Text(context.Text(test.IsActive ? "LabTests.Active" : "LabTests.Inactive"));
            }
        });
    }));

    private string Money(long amount) => MoneyFormat.Format(amount, context.Clinic?.CurrencyCode, context.Localization);

    private static IContainer HeaderCell(IContainer cell) =>
        cell.Background(Colors.Grey.Lighten3).Padding(6).DefaultTextStyle(style => style.SemiBold());

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);
}

using System.Collections.Generic;
using Mostawsef.Contracts.Patients;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Mostawsef.Desktop.Services.Printing;

/// <summary>The patient list: name, phone, gender and date of birth, in the order given. Notes stay out (medical data).</summary>
public sealed class PatientsDocument(PrintContext context, IReadOnlyList<PatientDto> patients) : IDocument
{
    private static readonly string[] Columns =
    [
        "Patients.Col.FullName", "Patients.Col.Phone", "Patients.Col.Gender", "Patients.Col.DateOfBirth",
    ];

    public void Compose(IDocumentContainer container) => ClinicPage.Compose(container, context, content => content.Column(column =>
    {
        column.Spacing(10);
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(context.Text("Nav.Patients")).FontSize(14).SemiBold();
            row.AutoItem().AlignBottom().Text(context.Localization.Format("Patients.Records", patients.Count)).FontColor(Colors.Grey.Darken1);
        });

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.RelativeColumn(2.5f);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                foreach (var key in Columns)
                {
                    header.Cell().Element(HeaderCell).Text(context.Text(key));
                }
            });

            foreach (var patient in patients)
            {
                table.Cell().Element(BodyCell).Text(patient.FullName);
                table.Cell().Element(BodyCell).Text(patient.Phone);
                table.Cell().Element(BodyCell).Text(context.Text("Gender." + patient.Gender));
                table.Cell().Element(BodyCell).Text(patient.DateOfBirth?.ToString("d", context.Localization.Culture) ?? string.Empty);
            }
        });
    }));

    private static IContainer HeaderCell(IContainer cell) =>
        cell.Background(Colors.Grey.Lighten3).Padding(6).DefaultTextStyle(style => style.SemiBold());

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);
}

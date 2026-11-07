using Mostawsef.Desktop.Converters;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Mostawsef.Desktop.Services.Printing;

/// <summary>Checks a printer and the layout: clinic header, Arabic and English text, a table and a money total.</summary>
public sealed class TestPageDocument(PrintContext context) : IDocument
{
    private static readonly (string Item, int Quantity, long Price)[] Lines =
    [
        ("كشف عام / General check-up", 1, 25000),
        ("تحليل صورة دم كاملة / CBC test", 1, 18050),
        ("قياس ضغط الدم / Blood pressure", 2, 2500),
    ];

    public void Compose(IDocumentContainer container) => ClinicPage.Compose(container, context, content => content.Column(column =>
    {
        column.Spacing(10);
        column.Item().Text(context.Text("Print.TestTitle")).FontSize(14).SemiBold();
        column.Item().Text(context.Text("Print.TestDescription"));
        column.Item().Text("مرحبًا بكم في مستوصف. هذا النص العربي يجب أن تظهر حروفه متصلة ومن اليمين إلى اليسار.");
        column.Item().Text("Welcome to Mostawsef. This English line should read from left to right.");

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.RelativeColumn(1);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text(context.Text("Print.Item"));
                header.Cell().Element(HeaderCell).Text(context.Text("Print.Quantity"));
                header.Cell().Element(HeaderCell).Text(context.Text("Print.Price"));
            });

            foreach (var (item, quantity, price) in Lines)
            {
                table.Cell().Element(BodyCell).Text(item);
                table.Cell().Element(BodyCell).Text(quantity.ToString());
                table.Cell().Element(BodyCell).Text(MoneyFormat.Format(price * quantity, context.Clinic?.CurrencyCode, context.Localization));
            }
        });

        var total = 0L;
        foreach (var (_, quantity, price) in Lines)
        {
            total += price * quantity;
        }

        column.Item().AlignRight().Text($"{context.Text("Print.Total")}: {MoneyFormat.Format(total, context.Clinic?.CurrencyCode, context.Localization)}")
            .SemiBold();
    }));

    private static IContainer HeaderCell(IContainer cell) =>
        cell.Background(Colors.Grey.Lighten3).Padding(6).DefaultTextStyle(style => style.SemiBold());

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);
}

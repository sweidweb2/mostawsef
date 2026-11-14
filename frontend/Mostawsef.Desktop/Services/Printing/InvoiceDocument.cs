using System;
using System.Linq;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Converters;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Mostawsef.Desktop.Services.Printing;

/// <summary>
/// An order's invoice on the shared clinic page: a title block (invoice number, date, status), who it is billed to,
/// the tests with their prices, the totals (what is owed, paid and still due) and the payments received.
/// Amounts use the order's own currency, so an old invoice never changes with the clinic's settings.
/// </summary>
public sealed class InvoiceDocument(PrintContext context, OrderDetailsDto order) : IDocument
{
    private static readonly string Accent = Colors.Blue.Darken3;

    public void Compose(IDocumentContainer container) => ClinicPage.Compose(container, context, content => content.Column(column =>
    {
        column.Spacing(14);
        column.Item().Element(Title);
        column.Item().Element(Parties);
        column.Item().Element(Tests);
        column.Item().Element(Totals);
        if (order.Payments.Count > 0)
        {
            column.Item().Element(Payments);
        }

        column.Item().PaddingTop(8).AlignCenter().Text(context.Text("Invoice.Thanks")).FontColor(Colors.Grey.Darken1);
    }));

    private string Money(long minorUnits) => MoneyFormat.Format(minorUnits, order.Currency, context.Localization);

    private string Local(DateTime value) =>
        DateTime.SpecifyKind(value, value.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : value.Kind)
            .ToLocalTime()
            .ToString("g", context.Localization.Culture);

    private void Title(IContainer container) => container.Row(row =>
    {
        row.RelativeItem().Column(column =>
        {
            column.Item().Text(context.Text("Invoice.Title")).FontSize(24).Bold().FontColor(Accent);
            column.Item().Text(order.Number).FontSize(12).SemiBold();
        });

        row.AutoItem().AlignMiddle().Border(1).BorderColor(StatusColor()).PaddingVertical(3).PaddingHorizontal(10)
            .Text(context.Text("OrderStatus." + order.Status)).FontColor(StatusColor()).SemiBold();
    });

    private string StatusColor() => order.Status switch
    {
        OrderStatuses.Paid => Colors.Green.Darken2,
        OrderStatuses.PartiallyPaid => Colors.Blue.Darken2,
        OrderStatuses.Pending => Colors.Orange.Darken2,
        _ => Colors.Red.Darken2,
    };

    private void Parties(IContainer container) => container.Row(row =>
    {
        row.Spacing(16);
        row.RelativeItem().Element(box => Box(box, context.Text("Invoice.BilledTo"), column =>
        {
            column.Item().Text(order.PatientName).SemiBold().FontSize(12);
            column.Item().Text(order.PatientPhone).DirectionFromLeftToRight();
        }));
        row.RelativeItem().Element(box => Box(box, context.Text("Invoice.Details"), column =>
        {
            Line(column, context.Text("Orders.Col.Number"), order.Number);
            Line(column, context.Text("Orders.Col.Date"), Local(order.CreatedAt));
            if (!string.IsNullOrWhiteSpace(order.CreatedBy))
            {
                Line(column, context.Text("OrderDetails.CreatedBy"), order.CreatedBy);
            }
        }));
    });

    private static void Box(IContainer container, string title, Action<ColumnDescriptor> body) =>
        container.Background(Colors.Grey.Lighten4).Padding(10).Column(column =>
        {
            column.Spacing(3);
            column.Item().Text(title).FontSize(8).SemiBold().FontColor(Colors.Grey.Darken1).LetterSpacing(0.05f);
            body(column);
        });

    private static void Line(ColumnDescriptor column, string label, string value) =>
        column.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken1);
            row.AutoItem().Text(value).DirectionFromLeftToRight();
        });

    private void Tests(IContainer container) => container.Column(column =>
    {
        column.Spacing(6);
        column.Item().Text(context.Localization.Format("OrderDetails.Tests", order.Items.Count)).FontSize(12).SemiBold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);
                columns.RelativeColumn(2);
                columns.RelativeColumn(6);
                columns.RelativeColumn(2.5f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text(context.Text("LabTests.Col.Code"));
                header.Cell().Element(HeaderCell).Text(context.Text("LabTests.Col.Name"));
                header.Cell().Element(HeaderCell).AlignRight().Text(context.Text("LabTests.Col.Price"));
            });

            var number = 0;
            foreach (var item in order.Items)
            {
                number++;
                table.Cell().Element(BodyCell).Text(number.ToString(context.Localization.Culture));
                table.Cell().Element(BodyCell).Text(item.Code).DirectionFromLeftToRight();
                table.Cell().Element(BodyCell).Text(item.Name);
                table.Cell().Element(BodyCell).AlignRight().Text(Money(item.Price)).DirectionFromLeftToRight();
            }
        });
    });

    private void Totals(IContainer container) => container.AlignRight().Width(250).Column(column =>
    {
        column.Spacing(4);
        var catalog = order.Items.Sum(i => i.Price);
        if (order.Addon != 0)
        {
            Amount(column, context.Text("Invoice.TestsTotal"), catalog);
            Amount(column, context.Text("Invoice.Addon"), order.Addon);
        }

        column.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(4).Element(row =>
            AmountRow(row, context.Text("Orders.Col.Total"), order.Total, bold: true, size: 12));
        Amount(column, context.Text("Orders.Col.Paid"), order.Paid);

        if (order.Balance < 0)
        {
            column.Item().Background(Colors.Blue.Lighten5).Padding(6).Element(row =>
                AmountRow(row, context.Text("Invoice.RefundDue"), -order.Balance, bold: true, size: 12, color: Colors.Blue.Darken3));
        }
        else
        {
            column.Item().Background(order.Balance == 0 ? Colors.Green.Lighten5 : Colors.Orange.Lighten5).Padding(6).Element(row =>
                AmountRow(row, context.Text("Invoice.BalanceDue"), order.Balance, bold: true, size: 12,
                    color: order.Balance == 0 ? Colors.Green.Darken3 : Colors.Orange.Darken3));
        }
    });

    private void Amount(ColumnDescriptor column, string label, long value) =>
        column.Item().Element(row => AmountRow(row, label, value));

    private void AmountRow(IContainer container, string label, long value, bool bold = false, float size = 10, string? color = null) =>
        container.Row(row =>
        {
            var left = row.RelativeItem().Text(label).FontSize(size);
            var right = row.AutoItem().Text(Money(value)).FontSize(size).DirectionFromLeftToRight();
            if (bold)
            {
                left.SemiBold();
                right.SemiBold();
            }

            if (color is not null)
            {
                left.FontColor(color);
                right.FontColor(color);
            }
        });

    private void Payments(IContainer container) => container.Column(column =>
    {
        column.Spacing(6);
        column.Item().Text(context.Text("Invoice.Payments")).FontSize(12).SemiBold();
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2.5f);
                columns.RelativeColumn(3);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text(context.Text("OrderDetails.Col.PaidAt"));
                header.Cell().Element(HeaderCell).Text(context.Text("OrderDetails.Col.Type"));
                header.Cell().Element(HeaderCell).Text(context.Text("NewOrder.PaymentMethod"));
                header.Cell().Element(HeaderCell).AlignRight().Text(context.Text("NewOrder.PaymentAmount"));
                header.Cell().Element(HeaderCell).Text(context.Text("NewOrder.PaymentNotes"));
            });

            foreach (var payment in order.Payments)
            {
                table.Cell().Element(BodyCell).Text(Local(payment.PaidAt));
                table.Cell().Element(BodyCell).Text(context.Text("PaymentType." + payment.Type));
                table.Cell().Element(BodyCell).Text(context.Text("PaymentMethod." + payment.Method));
                table.Cell().Element(BodyCell).AlignRight().Text(
                    (payment.Type == PaymentTypes.Refund ? "-" : string.Empty) + Money(payment.Amount)).DirectionFromLeftToRight();
                table.Cell().Element(BodyCell).Text(payment.Notes ?? string.Empty);
            }
        });
    });

    private static IContainer HeaderCell(IContainer cell) =>
        cell.Background(Accent).Padding(6).DefaultTextStyle(style => style.SemiBold().FontColor(Colors.White));

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6);
}

using System;
using Avalonia.Data.Converters;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Converters;

/// <summary>
/// Shared converters. Usage: Converter={x:Static conv:ValueConverters.Truncate}, ConverterParameter=60
/// with xmlns:conv="using:Mostawsef.Desktop.Converters". Translated labels use {l:TrKey ..., Prefix=...} instead.
/// </summary>
public static class ValueConverters
{
    /// <summary>Cuts text to ConverterParameter characters (default 80) on one line and adds "…". Show the full text in a tooltip.</summary>
    public static readonly IValueConverter Truncate = new TruncateConverter();

    /// <summary>UTC timestamp -> local date and time in the current language's format.</summary>
    public static readonly IValueConverter LocalDateTime = new FuncValueConverter<DateTime, string>(FormatLocal);

    /// <summary>Byte count -> short size label (B / KB / MB) for dump history.</summary>
    public static readonly IValueConverter ByteSize = new FuncValueConverter<long, string>(FormatByteSize);

    /// <summary>
    /// Minor units -> money text for DataGrid text columns (one binding, no MultiBinding per cell).
    /// Uses the live clinic currency. Prefer <see cref="OrderListTotal"/> / <see cref="OrderListPaid"/> for order rows.
    /// Forms and details keep {l:Money ...} so they still refresh on language/currency changes.
    /// </summary>
    public static readonly IValueConverter Money = new FuncValueConverter<long, string>(FormatMoney);

    /// <summary>Order list Total using the row's snapshotted <see cref="OrderListItemDto.Currency"/>.</summary>
    public static readonly IValueConverter OrderListTotal = new FuncValueConverter<OrderListItemDto?, string>(
        row => row is null ? string.Empty : FormatMoney(row.Total, row.Currency));

    /// <summary>Order list Paid using the row's snapshotted <see cref="OrderListItemDto.Currency"/>.</summary>
    public static readonly IValueConverter OrderListPaid = new FuncValueConverter<OrderListItemDto?, string>(
        row => row is null ? string.Empty : FormatMoney(row.Paid, row.Currency));

    /// <summary>"Warning" / "Error" / "Critical" -> Badge kind.</summary>
    public static readonly IValueConverter ErrorLevelToBadgeKind = new FuncValueConverter<string?, BadgeKind>(level => level switch
    {
        "Critical" => BadgeKind.Danger,
        "Error" => BadgeKind.Warning,
        "Warning" => BadgeKind.Info,
        _ => BadgeKind.Neutral,
    });

    /// <summary>Order status (OrderStatuses) -> Badge kind.</summary>
    public static readonly IValueConverter OrderStatusToBadgeKind = new FuncValueConverter<string?, BadgeKind>(status => status switch
    {
        OrderStatuses.Paid => BadgeKind.Success,
        OrderStatuses.PartiallyPaid => BadgeKind.Info,
        OrderStatuses.Pending => BadgeKind.Warning,
        OrderStatuses.Cancelled or OrderStatuses.Refunded => BadgeKind.Danger,
        _ => BadgeKind.Neutral,
    });

    /// <summary>
    /// Calendar dates (DateOnly, e.g. a birth date) for CalendarDatePicker, which works with DateTime:
    /// SelectedDate="{Binding BirthDate, Converter={x:Static conv:ValueConverters.DateOnly}}".
    /// </summary>
    public static readonly IValueConverter DateOnly = new DateOnlyConverter();

    /// <summary>Calendar date (DateOnly) -> short date in the current language's format; empty for null.</summary>
    public static readonly IValueConverter ShortDate = new FuncValueConverter<System.DateOnly?, string>(date =>
        date?.ToString("d", App.Services is null ? System.Globalization.CultureInfo.CurrentCulture : App.Localization.Culture) ?? string.Empty);

    /// <summary>A month's first day as "November 2026" in the app's language (dashboard month dropdown).</summary>
    public static readonly IValueConverter MonthYear = new FuncValueConverter<System.DateOnly?, string>(month =>
        month?.ToString("MMMM yyyy", App.Services is null ? System.Globalization.CultureInfo.CurrentCulture : App.Localization.Culture) ?? string.Empty);

    public static string FormatLocal(DateTime value) =>
        DateTime.SpecifyKind(value, value.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : value.Kind)
            .ToLocalTime()
            .ToString("g", App.Services is null ? System.Globalization.CultureInfo.CurrentCulture : App.Localization.Culture);

    public static string FormatByteSize(long bytes)
    {
        var culture = App.Services is null
            ? System.Globalization.CultureInfo.InvariantCulture
            : App.Localization.Culture;
        return bytes switch
        {
            < 1024 => string.Create(culture, $"{bytes} B"),
            < 1024 * 1024 => string.Create(culture, $"{bytes / 1024.0:0.#} KB"),
            _ => string.Create(culture, $"{bytes / (1024.0 * 1024.0):0.#} MB"),
        };
    }

    public static string FormatMoney(long minorUnits) => FormatMoney(minorUnits, currencyCode: null);

    public static string FormatMoney(long minorUnits, string? currencyCode)
    {
        if (App.Services is null)
        {
            return Mostawsef.Contracts.Common.Money.ToMajor(minorUnits)
                .ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        }

        return MoneyFormat.Format(minorUnits, currencyCode ?? Clinic.CurrencyCode, App.Localization);
    }

    private static ClinicContext? _clinic;
    private static ClinicContext Clinic => _clinic ??= App.Services!.GetRequiredService<ClinicContext>();

    private sealed class TruncateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var max = parameter is int i
                ? i
                : parameter is string text && int.TryParse(text, out var parsed) ? parsed : 80;
            if (value is not string source || source.Length == 0)
            {
                return value as string;
            }

            var needsFlat = source.AsSpan().IndexOfAny('\r', '\n') >= 0;
            if (!needsFlat && source.Length <= max)
            {
                return source;
            }

            var singleLine = needsFlat ? source.ReplaceLineEndings(" ") : source;
            if (singleLine.Length <= max)
            {
                return singleLine;
            }

            return string.Concat(singleLine.AsSpan(0, max).TrimEnd(), "…");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }

    private sealed class DateOnlyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is System.DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is DateTime dateTime ? System.DateOnly.FromDateTime(dateTime) : null;
    }
}

using System;
using Avalonia.Data.Converters;
using Mostawsef.Desktop.Components;

namespace Mostawsef.Desktop.Converters;

/// <summary>
/// Shared converters. Usage: Converter={x:Static conv:ValueConverters.Truncate}, ConverterParameter=60
/// with xmlns:conv="using:Mostawsef.Desktop.Converters". Translated labels use {l:TrKey ..., Prefix=...} instead.
/// </summary>
public static class ValueConverters
{
    /// <summary>Cuts text to ConverterParameter characters (default 80) on one line and adds "…". Show the full text in a tooltip.</summary>
    public static readonly IValueConverter Truncate = new FuncValueConverter<string?, object?, string?>((text, parameter) =>
    {
        var max = parameter is int i ? i : int.TryParse(parameter?.ToString(), out var parsed) ? parsed : 80;
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var singleLine = text.ReplaceLineEndings(" ");
        return singleLine.Length <= max ? singleLine : singleLine[..max].TrimEnd() + "…";
    });

    /// <summary>UTC timestamp -> local date and time in the current language's format.</summary>
    public static readonly IValueConverter LocalDateTime = new FuncValueConverter<DateTime, string>(FormatLocal);

    /// <summary>"Warning" / "Error" / "Critical" -> Badge kind.</summary>
    public static readonly IValueConverter ErrorLevelToBadgeKind = new FuncValueConverter<string?, BadgeKind>(level => level switch
    {
        "Critical" => BadgeKind.Danger,
        "Error" => BadgeKind.Warning,
        "Warning" => BadgeKind.Info,
        _ => BadgeKind.Neutral,
    });

    /// <summary>
    /// Calendar dates (DateOnly, e.g. a birth date) for CalendarDatePicker, which works with DateTime:
    /// SelectedDate="{Binding BirthDate, Converter={x:Static conv:ValueConverters.DateOnly}}".
    /// </summary>
    public static readonly IValueConverter DateOnly = new DateOnlyConverter();

    public static string FormatLocal(DateTime value) =>
        DateTime.SpecifyKind(value, value.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : value.Kind)
            .ToLocalTime()
            .ToString("g", App.Services is null ? System.Globalization.CultureInfo.CurrentCulture : App.Localization.Culture);

    private sealed class DateOnlyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is System.DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is DateTime dateTime ? System.DateOnly.FromDateTime(dateTime) : null;
    }
}

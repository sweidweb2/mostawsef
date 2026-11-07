using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Converters;

/// <summary>
/// Shared converters. Usage: Converter={x:Static conv:ValueConverters.Truncate}, ConverterParameter=60
/// with xmlns:conv="using:Mostawsef.Desktop.Converters".
/// </summary>
public static class ValueConverters
{
    /// <summary>Cuts text to ConverterParameter characters (default 80) and adds "…". Show the full text in a tooltip.</summary>
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

    /// <summary>UTC timestamp -> local date and time in the current culture.</summary>
    public static readonly IValueConverter LocalDateTime = new FuncValueConverter<DateTime, string>(value =>
        DateTime.SpecifyKind(value, value.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : value.Kind)
            .ToLocalTime()
            .ToString("g", LocalizationService.Instance.Culture));

    /// <summary>Activity code ("auth.signed_in") -> translated label (resource "Action.auth.signed_in"), or the code itself.</summary>
    public static readonly IValueConverter ActivityAction = new FuncValueConverter<string?, string?>(code =>
    {
        if (string.IsNullOrEmpty(code))
        {
            return code;
        }

        var key = "Action." + code;
        var label = LocalizationService.Instance.Get(key);
        return label == key ? code : label;
    });

    /// <summary>"Warning" / "Error" / "Critical" -> Badge kind.</summary>
    public static readonly IValueConverter ErrorLevelToBadgeKind = new FuncValueConverter<string?, BadgeKind>(level => level switch
    {
        "Critical" => BadgeKind.Danger,
        "Error" => BadgeKind.Warning,
        "Warning" => BadgeKind.Info,
        _ => BadgeKind.Neutral,
    });

    /// <summary>"Warning" / "Error" / "Critical" -> translated label.</summary>
    public static readonly IValueConverter ErrorLevelLabel = new FuncValueConverter<string?, string?>(level =>
        level is null ? null : LocalizationService.Instance.Get("ErrorLevel." + level));
}

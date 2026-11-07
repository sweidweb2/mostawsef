using System;
using System.Text.Json;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Converters;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>Content of the "view" modal for one activity log record.</summary>
public class ActivityLogDetailsViewModel(ActivityLogDto log) : ViewModelBase
{
    public ActivityLogDto Log { get; } = log;

    public string OccurredAt => LogFormat.Time(Log.OccurredAt);

    public string? ActionLabel => (string?)ValueConverters.ActivityAction.Convert(Log.Action, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);

    public string? Entity => Log.EntityType is null ? null : $"{Log.EntityType} {Log.EntityId}".Trim();

    public string? Details => LogFormat.PrettyJson(Log.Details);
}

/// <summary>Content of the "view" modal for one system error record.</summary>
public class SystemErrorDetailsViewModel(SystemErrorDto error) : ViewModelBase
{
    public SystemErrorDto Error { get; } = error;

    public string OccurredAt => LogFormat.Time(Error.OccurredAt);

    public string? LevelLabel => (string?)ValueConverters.ErrorLevelLabel.Convert(Error.Level, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);

    public string? Request => Error.RequestPath is null ? null : $"{Error.RequestMethod} {Error.RequestPath}".Trim();

    public string? StatusCode => Error.StatusCode?.ToString();

    public string? Details => LogFormat.PrettyJson(Error.Details);
}

internal static class LogFormat
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string Time(DateTime utc) =>
        (string)ValueConverters.LocalDateTime.Convert(utc, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture)!;

    public static string? PrettyJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, Indented);
        }
        catch (JsonException)
        {
            return json;
        }
    }
}

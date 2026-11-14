using System.Text.Json;

namespace Mostawsef.Infrastructure.Services.Logging;

/// <summary>How the Details object of an activity or error entry is stored as JSON.</summary>
internal static class LogJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

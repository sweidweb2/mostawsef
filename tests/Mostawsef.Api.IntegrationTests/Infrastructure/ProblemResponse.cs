using System.Net;
using System.Text.Json;

namespace Mostawsef.Api.IntegrationTests.Infrastructure;

/// <summary>A problem response as the desktop app reads it: status, "code" and the per-field error codes.</summary>
public sealed record ProblemResponse(HttpStatusCode Status, string? Code, IReadOnlyDictionary<string, string[]> Errors)
{
    public static async Task<ProblemResponse> ReadAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = body.RootElement;

        var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
        var errors = root.TryGetProperty("errors", out var e)
            ? e.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray(),
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        return new ProblemResponse(response.StatusCode, code, errors);
    }
}

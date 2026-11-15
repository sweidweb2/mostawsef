using System.Security.Cryptography;
using System.Text.Json;

namespace Mostawsef.Api;

/// <summary>
/// Per-clinic file next to the API (<c>appsettings.Clinic.json</c>). Holds the JWT signing key so each
/// install gets a unique secret that is never committed. Development and Testing must set <c>Jwt:Key</c>
/// themselves (they do not auto-create this file).
/// </summary>
public static class ClinicLocalConfig
{
    public const string FileName = "appsettings.Clinic.json";

    private static readonly JsonSerializerOptions ClinicJson = new() { WriteIndented = true };

    /// <summary>
    /// Loads an existing clinic file if present. On Production (and other non-dev/test hosts), when
    /// <c>Jwt:Key</c> is still missing or too short, writes a new random key into the clinic file and loads it.
    /// </summary>
    public static void Apply(WebApplicationBuilder builder)
    {
        var path = Path.Combine(builder.Environment.ContentRootPath, FileName);
        if (File.Exists(path))
        {
            builder.Configuration.AddJsonFile(path, optional: true, reloadOnChange: false);
        }

        if (HasUsableJwtKey(builder.Configuration["Jwt:Key"]))
        {
            return;
        }

        if (!ShouldAutoCreateKey(builder.Environment.EnvironmentName))
        {
            return;
        }

        var key = CreateAndWriteJwtKey(path);
        builder.Configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
        // Ensure the in-memory view sees the new key even if another source still has an empty string.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key });
    }

    public static bool HasUsableJwtKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length >= 32;

    public static bool ShouldAutoCreateKey(string environmentName) =>
        !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    /// <summary>Writes a new random key to <paramref name="clinicFilePath"/> and returns it.</summary>
    public static string CreateAndWriteJwtKey(string clinicFilePath)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var json = JsonSerializer.Serialize(
            new Dictionary<string, Dictionary<string, string>>
            {
                ["Jwt"] = new Dictionary<string, string> { ["Key"] = key },
            },
            ClinicJson);
        File.WriteAllText(clinicFilePath, json);
        return key;
    }
}

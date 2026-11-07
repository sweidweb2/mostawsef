using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mostawsef.Desktop.Services.Local;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public SettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mostawsef", "settings.json"))
    {
    }

    public SettingsService(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        Current = Load();
    }

    public AppSettings Current { get; }

    public void Save()
    {
        File.WriteAllText(_filePath, JsonSerializer.Serialize(Current, JsonOptions));
    }

    private AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }
}

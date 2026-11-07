using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// Loads settings.json once; Save() writes it on a background thread so theme/language switches never wait on disk.
/// Rapid saves collapse into one write of the latest values. Call Flush() before the app exits.
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly object _gate = new();
    private string? _pendingJson;
    private Task _writer = Task.CompletedTask;

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
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        lock (_gate)
        {
            var writeQueued = _pendingJson is not null;
            _pendingJson = json;
            if (!writeQueued)
            {
                _writer = _writer.ContinueWith(_ => WritePending(), TaskScheduler.Default);
            }
        }
    }

    /// <summary>Waits until every saved change is on disk.</summary>
    public void Flush()
    {
        Task writer;
        lock (_gate)
        {
            writer = _writer;
        }

        writer.Wait();
    }

    private void WritePending()
    {
        string? json;
        lock (_gate)
        {
            json = _pendingJson;
            _pendingJson = null;
        }

        if (json is null)
        {
            return;
        }

        var tempPath = _filePath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preferences are best effort: a locked or read-only file must not disturb the user.
        }
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

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Desktop.Services.Diagnostics;

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
    private readonly Lazy<ErrorReporter>? _errors;
    private readonly object _gate = new();
    private string? _pendingJson;
    private Task _writer = Task.CompletedTask;
    private Exception? _loadFault;

    public SettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mostawsef", "settings.json"))
    {
    }

    public SettingsService(string filePath, Lazy<ErrorReporter>? errors = null)
    {
        _filePath = filePath;
        _errors = errors;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        Current = Load();
    }

    public AppSettings Current { get; }

    public void Save()
    {
        ReportLoadFaultIfAny();
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

    /// <summary>Waits until every saved change is on disk, but never longer than <see cref="FlushTimeout"/> (a stalled disk must not hang exit).</summary>
    public void Flush()
    {
        Task writer;
        lock (_gate)
        {
            writer = _writer;
        }

        writer.Wait(FlushTimeout);
    }

    private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Sends a deferred corrupt-file fault once DI can resolve <see cref="ErrorReporter"/>.</summary>
    public void ReportLoadFaultIfAny()
    {
        var fault = Interlocked.Exchange(ref _loadFault, null);
        if (fault is not null)
        {
            _errors?.Value.Report(fault, context: "Loading settings.json");
        }
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
        catch (Exception ex)
        {
            // Preferences are best effort: a locked, read-only or otherwise unwritable file must not disturb the user.
            _errors?.Value.Report(ex, context: "Saving settings.json");
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
        catch (JsonException ex)
        {
            // Cannot resolve ErrorReporter here: this runs inside the SettingsService DI factory.
            _loadFault = ex;
            return new AppSettings();
        }
    }
}

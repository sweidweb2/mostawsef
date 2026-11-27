using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Services.Updates;

/// <summary>
/// Checks the public GitHub <c>latest.json</c> feed and exposes an update banner for the portal.
/// Offline / bad feed → silent (no banner, no crash). MVP: Download opens the Setup URL in the browser.
/// </summary>
public partial class UpdateChecker : ObservableObject
{
    public static readonly Uri DefaultFeedUrl =
        new("https://github.com/sweidweb2/mostawsef/releases/latest/download/latest.json");

    /// <summary>Do not re-hit the feed more often than this (portal re-entry, language churn, etc.).</summary>
    public static TimeSpan MinCheckInterval { get; set; } = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly LocalizationService _localization;
    private readonly Action<string> _openUrl;
    private readonly Uri _feedUrl;
    private readonly object _gate = new();
    private DateTimeOffset _lastCheckAttempt = DateTimeOffset.MinValue;
    private UpdateFeed? _feed;

    public UpdateChecker(
        HttpMessageHandler handler,
        LocalizationService localization,
        Action<string>? openUrl = null,
        Uri? feedUrl = null)
    {
        _http = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        _localization = localization;
        _openUrl = openUrl ?? OpenWithDefaultBrowser;
        _feedUrl = feedUrl ?? DefaultFeedUrl;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    public partial bool IsAvailable { get; private set; }

    /// <summary>Fully translated banner text (version + notes). Empty when no update.</summary>
    [ObservableProperty]
    public partial string BannerText { get; private set; } = string.Empty;

    public string? DownloadUrl { get; private set; }

    /// <summary>How many feed HTTP calls succeeded or failed (tests).</summary>
    public int CheckAttempts { get; private set; }

    public async Task CheckNowAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!force && DateTimeOffset.UtcNow - _lastCheckAttempt < MinCheckInterval)
            {
                return;
            }

            _lastCheckAttempt = DateTimeOffset.UtcNow;
        }

        CheckAttempts++;

        try
        {
            using var response = await _http.GetAsync(_feedUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var feed = await response.Content.ReadFromJsonAsync<UpdateFeed>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (feed is null
                || string.IsNullOrWhiteSpace(feed.Version)
                || string.IsNullOrWhiteSpace(feed.Url)
                || !Uri.TryCreate(feed.Url, UriKind.Absolute, out _))
            {
                return;
            }

            if (AppVersion.Compare(feed.Version, AppVersion.Current) <= 0)
            {
                await SetUnavailableAsync().ConfigureAwait(false);
                return;
            }

            await ApplyFeedAsync(feed).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Offline, timeout, HTML error page, bad JSON — stay quiet.
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private void Download()
    {
        if (DownloadUrl is { Length: > 0 } url)
        {
            _openUrl(url);
        }
    }

    private bool CanDownload() => IsAvailable && !string.IsNullOrWhiteSpace(DownloadUrl);

    private async Task ApplyFeedAsync(UpdateFeed feed)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _feed = feed;
            DownloadUrl = feed.Url;
            BannerText = BuildBannerText(feed);
            IsAvailable = true;
        });
    }

    private async Task SetUnavailableAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _feed = null;
            DownloadUrl = null;
            BannerText = string.Empty;
            IsAvailable = false;
        });
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_feed is null || !IsAvailable)
        {
            return;
        }

        BannerText = BuildBannerText(_feed);
    }

    private string BuildBannerText(UpdateFeed feed)
    {
        var notes = _localization.Language == AppLanguage.Arabic
            ? feed.NotesAr
            : feed.NotesEn;
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        return notes is null
            ? _localization.Format("Update.Available", feed.Version)
            : _localization.Format("Update.AvailableWithNotes", feed.Version, notes);
    }

    private static void OpenWithDefaultBrowser(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}

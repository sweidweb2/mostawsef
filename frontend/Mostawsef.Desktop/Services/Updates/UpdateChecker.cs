using System;
using System.IO;
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
/// Checks the public GitHub <c>latest.json</c> feed and holds the update banner's state. Install hands the download
/// to <see cref="UpdateDownloader"/>, then the elevated silent Setup to a second process (<see cref="IUpdateHost"/>).
/// </summary>
public sealed partial class UpdateChecker : ObservableObject, IDisposable
{
    public static readonly Uri DefaultFeedUrl =
        new("https://github.com/sweidweb2/mostawsef/releases/latest/download/latest.json");

    /// <summary>
    /// A feed may only point at a Setup in this repository's GitHub releases over https. Anything else is ignored,
    /// so a tampered or misconfigured feed cannot send the app to another host (the Setup runs elevated).
    /// </summary>
    public const string AllowedDownloadPrefix = "https://github.com/sweidweb2/mostawsef/releases/download/";

    /// <summary>Do not re-hit the feed more often than this (portal re-entry, sign-out and back, etc.).</summary>
    public static readonly TimeSpan MinCheckInterval = TimeSpan.FromHours(6);

    private static readonly TimeSpan FeedTimeout = TimeSpan.FromSeconds(8);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly LocalizationService _localization;
    private readonly IUpdateHost _host;
    private readonly UpdateDownloader _downloader;
    private readonly TimeProvider _time;
    private readonly Uri _feedUrl;
    private readonly Lock _gate = new();
    private DateTimeOffset _lastCheckAttempt = DateTimeOffset.MinValue;
    private UpdateFeed? _feed;
    private CancellationTokenSource? _installCts;

    public UpdateChecker(
        HttpMessageHandler handler,
        LocalizationService localization,
        IUpdateHost host,
        TimeProvider? time = null,
        Uri? feedUrl = null)
    {
        _http = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _localization = localization;
        _host = host;
        _downloader = new UpdateDownloader(_http, host.DownloadDirectory);
        _time = time ?? TimeProvider.System;
        _feedUrl = feedUrl ?? DefaultFeedUrl;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyPropertyChangedFor(nameof(ShowBanner))]
    public partial bool IsAvailable { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelInstallCommand))]
    [NotifyPropertyChangedFor(nameof(ShowBanner))]
    [NotifyPropertyChangedFor(nameof(ShowProgress))]
    public partial bool IsInstalling { get; private set; }

    /// <summary>0–100 while downloading; null when not downloading.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProgress))]
    public partial double? ProgressPercent { get; private set; }

    /// <summary>Status line under the banner while installing (already translated).</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>Fully translated banner text (version + notes). Empty when no update.</summary>
    [ObservableProperty]
    public partial string BannerText { get; private set; } = string.Empty;

    /// <summary>Last error for toasts / banner (already translated). Cleared on a new install attempt.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    public bool ShowBanner => IsAvailable || IsInstalling;

    public bool ShowProgress => IsInstalling && ProgressPercent is not null;

    public string? DownloadUrl { get; private set; }

    /// <summary>Optional: portal confirms dirty forms before the app exits. Return false to abort.</summary>
    public Func<Task<bool>>? ConfirmLeaveAsync { get; set; }

    /// <summary>
    /// Reads the feed (at most once per <see cref="MinCheckInterval"/> unless <paramref name="force"/>). An offline PC,
    /// a timeout, an error page or a malformed feed is expected and only means "no update"; anything else is thrown.
    /// </summary>
    public async Task CheckNowAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            if (!force && now - _lastCheckAttempt < MinCheckInterval)
            {
                return;
            }

            _lastCheckAttempt = now;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(FeedTimeout);

            using var response = await _http.GetAsync(_feedUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var feed = await response.Content.ReadFromJsonAsync<UpdateFeed>(JsonOptions, timeout.Token)
                .ConfigureAwait(false);
            if (feed is null
                || string.IsNullOrWhiteSpace(feed.Version)
                || string.IsNullOrWhiteSpace(feed.Url)
                || string.IsNullOrWhiteSpace(feed.Sha256)
                || !IsAllowedDownloadUrl(feed.Url))
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Feed timeout: try again on the next check.
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException)
        {
            // Offline, connection dropped, or an HTML error page instead of JSON: no update to show.
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        if (_feed is null || IsInstalling)
        {
            return;
        }

        var feed = _feed;
        ErrorText = null;
        _installCts?.Cancel();
        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        var ct = _installCts.Token;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsInstalling = true;
            ProgressPercent = 0;
            StatusText = _localization.Get("Update.Preparing");
        });

        try
        {
            if (ConfirmLeaveAsync is not null && !await ConfirmLeaveAsync().ConfigureAwait(false))
            {
                await FinishInstallUiAsync().ConfigureAwait(false);
                return;
            }

            await SetStatusAsync(_localization.Format("Update.Downloading", 0)).ConfigureAwait(false);
            var setupPath = await _downloader.DownloadAsync(feed, ReportProgressAsync, ct).ConfigureAwait(false);
            if (setupPath is null)
            {
                await FailAsync(_localization.Get("Update.ChecksumFailed")).ConfigureAwait(false);
                return;
            }

            await SetStatusAsync(_localization.Get("Update.StartingSetup")).ConfigureAwait(false);
            _host.StartApplyUpdate(setupPath, feed.Sha256.Trim(), Environment.ProcessId);
            _host.ExitApplication();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await FinishInstallUiAsync(status: _localization.Get("Update.Cancelled")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Shown to the user in the banner (download failed, disk full, Setup could not start).
            await FailAsync(_localization.Format("Update.Failed", ShortError(ex))).ConfigureAwait(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelInstall))]
    private void CancelInstall()
    {
        _installCts?.Cancel();
    }

    private bool CanInstall() => IsAvailable && !IsInstalling && !string.IsNullOrWhiteSpace(DownloadUrl);

    private bool CanCancelInstall() => IsInstalling && ProgressPercent is not null;

    /// <summary>https, no user info or custom port, and under <see cref="AllowedDownloadPrefix"/>.</summary>
    public static bool IsAllowedDownloadUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.AbsoluteUri.StartsWith(AllowedDownloadPrefix, StringComparison.Ordinal);

    public void Dispose()
    {
        _localization.LanguageChanged -= OnLanguageChanged;
        _installCts?.Cancel();
        _installCts?.Dispose();
        _http.Dispose();
    }

    private async Task ReportProgressAsync(int percent) =>
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProgressPercent = percent;
            StatusText = _localization.Format("Update.Downloading", percent);
        });

    private async Task ApplyFeedAsync(UpdateFeed feed)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _feed = feed;
            DownloadUrl = feed.Url;
            BannerText = BuildBannerText(feed);
            IsAvailable = true;
            ErrorText = null;
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

    private async Task FailAsync(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ErrorText = message;
            StatusText = message;
            IsInstalling = false;
            ProgressPercent = null;
        });
    }

    private async Task FinishInstallUiAsync(string? status = null)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsInstalling = false;
            ProgressPercent = null;
            StatusText = status ?? string.Empty;
        });
    }

    private async Task SetStatusAsync(string status) =>
        await Dispatcher.UIThread.InvokeAsync(() => { StatusText = status; });

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

    private static string ShortError(Exception ex) =>
        string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
}

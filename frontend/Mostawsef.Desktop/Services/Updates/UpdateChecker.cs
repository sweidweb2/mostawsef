using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Services.Updates;

/// <summary>
/// Checks the public GitHub <c>latest.json</c> feed and installs an update Setup in-app
/// (download → sha256 → elevated silent Setup via a second process → this app exits → relaunch).
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
    private readonly IUpdateHost _host;
    private readonly Uri _feedUrl;
    private readonly object _gate = new();
    private DateTimeOffset _lastCheckAttempt = DateTimeOffset.MinValue;
    private UpdateFeed? _feed;
    private CancellationTokenSource? _installCts;

    public UpdateChecker(
        HttpMessageHandler handler,
        LocalizationService localization,
        IUpdateHost host,
        Uri? feedUrl = null)
    {
        _http = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _localization = localization;
        _host = host;
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

    /// <summary>How many feed HTTP calls succeeded or failed (tests).</summary>
    public int CheckAttempts { get; private set; }

    /// <summary>Setup paths handed to the applicator (tests).</summary>
    public string? LastSetupPath { get; private set; }

    /// <summary>Optional: portal confirms dirty forms before the app exits. Return false to abort.</summary>
    public Func<Task<bool>>? ConfirmLeaveAsync { get; set; }

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
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));

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

            var setupPath = await DownloadSetupAsync(feed, ct).ConfigureAwait(false);
            await SetStatusAsync(_localization.Get("Update.Verifying")).ConfigureAwait(false);
            if (!await VerifySha256Async(setupPath, feed.Sha256, ct).ConfigureAwait(false))
            {
                TryDelete(setupPath);
                await FailAsync(_localization.Get("Update.ChecksumFailed")).ConfigureAwait(false);
                return;
            }

            await SetStatusAsync(_localization.Get("Update.StartingSetup")).ConfigureAwait(false);
            LastSetupPath = setupPath;
            _host.StartApplyUpdate(setupPath, Environment.ProcessId);
            _host.ExitApplication();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await FinishInstallUiAsync(status: _localization.Get("Update.Cancelled")).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
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

    private async Task<string> DownloadSetupAsync(UpdateFeed feed, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_host.DownloadDirectory);
        var fileName = $"Mostawsef-Setup-{SanitizeFileVersion(feed.Version)}.exe";
        var path = Path.Combine(_host.DownloadDirectory, fileName);
        var tempPath = path + ".partial";
        TryDelete(tempPath);
        TryDelete(path);

        await SetStatusAsync(_localization.Format("Update.Downloading", 0)).ConfigureAwait(false);

        using var response = await _http.GetAsync(new Uri(feed.Url), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var local = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            var lastReported = -1;
            while ((read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                readTotal += read;
                if (total > 0)
                {
                    var percent = (int)Math.Clamp(readTotal * 100 / total, 0, 100);
                    if (percent != lastReported)
                    {
                        lastReported = percent;
                        var p = percent;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ProgressPercent = p;
                            StatusText = _localization.Format("Update.Downloading", p);
                        });
                    }
                }
            }
        }

        File.Move(tempPath, path, overwrite: true);
        await Dispatcher.UIThread.InvokeAsync(() => { ProgressPercent = 100; });
        return path;
    }

    private static async Task<bool> VerifySha256Async(string path, string expectedHex, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actual = Convert.ToHexString(hash);
        return string.Equals(actual, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

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

    private static string SanitizeFileVersion(string version)
    {
        Span<char> buffer = stackalloc char[version.Length];
        var n = 0;
        foreach (var c in version)
        {
            buffer[n++] = char.IsAsciiLetterOrDigit(c) || c is '.' or '-' ? c : '_';
        }

        return new string(buffer[..n]);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort.
        }
    }

    private static string ShortError(Exception ex) =>
        string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
}

using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Mostawsef.Desktop.Services.Updates;

/// <summary>Downloads a feed's Setup into a folder and checks its SHA-256. No UI state; <see cref="UpdateChecker"/> owns that.</summary>
public sealed class UpdateDownloader(HttpClient http, string directory)
{
    private const int BufferSize = 81920;

    /// <summary>
    /// The downloaded Setup's path when its hash matches <see cref="UpdateFeed.Sha256"/>; null (file deleted) when it
    /// does not. <paramref name="onProgress"/> gets 0-100 once per whole percent, when the size is known.
    /// </summary>
    public async Task<string?> DownloadAsync(UpdateFeed feed, Func<int, Task> onProgress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"Mostawsef-Setup-{SanitizeFileVersion(feed.Version)}.exe");
        var tempPath = path + ".partial";
        TryDelete(tempPath);
        TryDelete(path);

        using var response = await http.GetAsync(new Uri(feed.Url), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var local = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
        {
            var buffer = new byte[BufferSize];
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
                        await onProgress(percent).ConfigureAwait(false);
                    }
                }
            }
        }

        File.Move(tempPath, path, overwrite: true);
        if (await HashMatchesAsync(path, feed.Sha256, cancellationToken).ConfigureAwait(false))
        {
            return path;
        }

        TryDelete(path);
        return null;
    }

    private static async Task<bool> HashMatchesAsync(string path, string expectedHex, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return UpdateApplicator.HashMatches(hash, expectedHex);
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
            File.Delete(path);
        }
        catch (IOException)
        {
            // Still open elsewhere: the next download replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: best-effort cleanup of our own download folder.
        }
    }
}

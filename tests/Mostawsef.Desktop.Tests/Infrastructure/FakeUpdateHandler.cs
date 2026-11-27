using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mostawsef.Desktop.Services.Updates;

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>Stands in for GitHub update feed + Setup download HTTP calls.</summary>
public sealed class FakeUpdateHandler : HttpMessageHandler
{
    public string? JsonBody { get; set; }

    public byte[]? FileBody { get; set; }

    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

    public Exception? Error { get; set; }

    public int RequestCount { get; private set; }

    public int FileRequestCount { get; private set; }

    public void RespondWith(UpdateFeed feed) =>
        JsonBody = JsonSerializer.Serialize(feed);

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public UpdateFeed RespondWithSetup(string version, byte[] setupBytes, string? notesEn = "notes", string? notesAr = null)
    {
        FileBody = setupBytes;
        var feed = new UpdateFeed(
            version,
            $"{UpdateChecker.AllowedDownloadPrefix}v{version}/Mostawsef-Setup-{version}.exe",
            Sha256Hex(setupBytes),
            notesEn,
            notesAr);
        RespondWith(feed);
        return feed;
    }

    public void FailWithNetworkError() => Error = new HttpRequestException("Connection refused");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        if (Error is not null)
        {
            throw Error;
        }

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (FileBody is not null && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            FileRequestCount++;
            return Task.FromResult(new HttpResponseMessage(StatusCode)
            {
                Content = new ByteArrayContent(FileBody)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream") },
                },
            });
        }

        return Task.FromResult(new HttpResponseMessage(StatusCode)
        {
            Content = new StringContent(JsonBody ?? "{}", Encoding.UTF8, "application/json"),
        });
    }
}

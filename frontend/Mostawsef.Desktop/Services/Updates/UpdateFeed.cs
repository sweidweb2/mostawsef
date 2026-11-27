namespace Mostawsef.Desktop.Services.Updates;

/// <summary>Payload of GitHub Release <c>latest.json</c> (Phase 3 feed).</summary>
public sealed record UpdateFeed(
    string Version,
    string Url,
    string Sha256,
    string? NotesEn,
    string? NotesAr);

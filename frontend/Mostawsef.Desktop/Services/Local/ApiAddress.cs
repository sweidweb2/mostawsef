using System;

namespace Mostawsef.Desktop.Services.Local;

public static class ApiAddress
{
    /// <summary>
    /// True when the address is plain http to another machine: the password, the token and the patient data would cross
    /// the network unencrypted. An address on this PC (localhost, 127.x, ::1) or any https address is fine.
    /// An address that cannot be parsed is not judged here: the HTTP client reports it.
    /// </summary>
    public static bool IsInsecureRemote(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && !uri.IsLoopback;
}

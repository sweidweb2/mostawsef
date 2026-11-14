using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Tests.Local;

public class ApiAddressTests
{
    [Theory]
    [InlineData("http://192.168.1.20:5270", true)]
    [InlineData("http://clinic-server:5270", true)]
    [InlineData("http://localhost:5270", false)]
    [InlineData("http://127.0.0.1:5270", false)]
    [InlineData("http://[::1]:5270", false)]
    [InlineData("https://192.168.1.20:7027", false)]
    [InlineData("not a url", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Flags_only_plain_http_to_another_machine(string? address, bool expected) =>
        Assert.Equal(expected, ApiAddress.IsInsecureRemote(address));
}

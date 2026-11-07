using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Mostawsef.Api.IntegrationTests.Infrastructure;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class JwtConfigurationTests(ApiFactory factory)
{
    [Theory]
    [InlineData("")]
    [InlineData("too-short-signing-key")]
    public void Api_does_not_start_with_a_missing_or_short_signing_key(string key)
    {
        using var badFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key })));

        var error = Record.Exception(() => badFactory.CreateClient());

        Assert.NotNull(error);
        Assert.True(error.ToString().Contains("Jwt:Key must be"), error.ToString());
    }
}

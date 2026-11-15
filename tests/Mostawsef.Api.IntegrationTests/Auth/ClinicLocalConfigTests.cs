using Mostawsef.Api;

namespace Mostawsef.Api.IntegrationTests.Auth;

public class ClinicLocalConfigTests
{
    [Fact]
    public void HasUsableJwtKey_requires_at_least_32_characters()
    {
        Assert.False(ClinicLocalConfig.HasUsableJwtKey(null));
        Assert.False(ClinicLocalConfig.HasUsableJwtKey(""));
        Assert.False(ClinicLocalConfig.HasUsableJwtKey("too-short-signing-key"));
        Assert.True(ClinicLocalConfig.HasUsableJwtKey("dev-only-signing-key-change-me-0123456789abcdef"));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    [InlineData("Testing", false)]
    public void Only_non_dev_hosts_auto_create_a_clinic_key(string environment, bool expected) =>
        Assert.Equal(expected, ClinicLocalConfig.ShouldAutoCreateKey(environment));

    [Fact]
    public void CreateAndWriteJwtKey_persists_a_usable_key()
    {
        var dir = Directory.CreateTempSubdirectory("mostawsef-clinic-");
        try
        {
            var path = Path.Combine(dir.FullName, ClinicLocalConfig.FileName);
            var key = ClinicLocalConfig.CreateAndWriteJwtKey(path);

            Assert.True(ClinicLocalConfig.HasUsableJwtKey(key));
            Assert.True(File.Exists(path));
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(key, doc.RootElement.GetProperty("Jwt").GetProperty("Key").GetString());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}

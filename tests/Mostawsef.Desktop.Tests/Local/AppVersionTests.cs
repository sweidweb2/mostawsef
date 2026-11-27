using System.Reflection;
using System.Reflection.Emit;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Tests.Local;

public class AppVersionTests
{
    [Fact]
    public void Current_is_a_semver_from_the_desktop_assembly()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+", AppVersion.Current);
        Assert.Equal(AppVersion.Read(typeof(App).Assembly), AppVersion.Current);
    }

    [Fact]
    public void Read_strips_source_link_metadata_after_plus()
    {
        var assembly = EmitInformationalVersion("1.2.3+abcdef");
        Assert.Equal("1.2.3", AppVersion.Read(assembly));
    }

    private static AssemblyBuilder EmitInformationalVersion(string informationalVersion)
    {
        var assemblyName = new AssemblyName($"AppVersionTest_{Guid.NewGuid():N}");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run);
        var attributeCtor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
        assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(attributeCtor, [informationalVersion]));
        assemblyBuilder.DefineDynamicModule("Main");
        return assemblyBuilder;
    }
}

using Mostawsef.Contracts.Common;

namespace Mostawsef.Api.IntegrationTests.Contracts;

public class ContractsJsonContextTests
{
    /// <summary>
    /// Every request/response type of the API must be in <see cref="ContractsJsonContext"/> (compile-time JSON metadata);
    /// a missing one still works through reflection, but its first call is slower. Fails with the names to add.
    /// </summary>
    [Fact]
    public void Every_request_and_response_type_is_listed()
    {
        var wireTypes = typeof(ContractsJsonContext).Assembly.GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } or { IsValueType: true, IsEnum: false })
            .Where(t => !t.IsGenericTypeDefinition && !t.IsNested)
            .Where(t => t.Name.EndsWith("Request", StringComparison.Ordinal)
                        || t.Name.EndsWith("Response", StringComparison.Ordinal)
                        || t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(wireTypes);

        var missing = wireTypes
            .Where(t => ContractsJsonContext.Default.GetTypeInfo(t) is null)
            .Select(t => t.FullName)
            .Order()
            .ToList();

        Assert.True(missing.Count == 0, "Add to ContractsJsonContext: " + string.Join(", ", missing));
    }
}

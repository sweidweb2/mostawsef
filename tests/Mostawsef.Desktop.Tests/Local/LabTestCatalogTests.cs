using System.Net.Http;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Local;

[Collection(UiTestCollection.Name)]
public class LabTestCatalogTests : UiTestBase
{
    private const string CatalogPath = "/api/lab-tests/all";

    private static readonly LabTestDto Cbc = new(Guid.NewGuid(), "CBC", "Complete blood count", 15000, 9000, true, default, null, null, 0);
    private static readonly LabTestDto Old = new(Guid.NewGuid(), "OLD", "Retired", 1000, 500, false, default, null, null, 0);

    [AvaloniaFact]
    public async Task The_catalog_is_one_request_keeps_active_tests_and_is_reused()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(CatalogPath, (IReadOnlyList<LabTestDto>)[Cbc, Old]);
        var catalog = app.Get<LabTestCatalog>();

        var first = await catalog.GetAsync(CancellationToken.None);
        await catalog.GetAsync(CancellationToken.None);

        Assert.Equal([Cbc.Id], first.Value!.Select(t => t.Id));
        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, CatalogPath));
    }

    [AvaloniaFact]
    public async Task A_catalog_download_that_finishes_after_sign_out_is_not_kept()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Api.On(HttpMethod.Get, CatalogPath, async (_, _) =>
        {
            await release.Task;
            return FakeApiHandler.Json<IReadOnlyList<LabTestDto>>([Cbc]);
        });
        var catalog = app.Get<LabTestCatalog>();

        var pending = catalog.GetAsync(CancellationToken.None);
        await UiWait.UntilAsync(() => app.Api.RequestsTo(HttpMethod.Get, CatalogPath).Any());
        app.Session.SignOut();
        release.SetResult();
        await pending;

        app.SignInAs(Roles.Admin);
        await catalog.GetAsync(CancellationToken.None);

        Assert.Equal(2, app.Api.RequestsTo(HttpMethod.Get, CatalogPath).Count());
    }
}

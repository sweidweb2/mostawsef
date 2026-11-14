using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Shell;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Patients;

[Collection(UiTestCollection.Name)]
public class RowNumberTests : UiTestBase
{
    private static List<PatientDto> Page(int page, int count = 40) =>
        Enumerable.Range(0, count).Select(i => Mona with { Id = Guid.NewGuid(), FullName = $"P{page}-{i}" }).ToList();

    /// <summary>Every drawn row shows FirstRowNumber + its real position in the page; returns how many rows were drawn.</summary>
    private static int CheckNumbers(Window window, PatientsViewModel patients)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var rows = window.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            var shown = row.GetVisualDescendants().OfType<RowNumber>().Single().Text;
            var expected = (patients.FirstRowNumber + patients.Items.IndexOf((PatientDto)row.DataContext!))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expected, shown);
        }

        return rows.Count;
    }

    private static void ScrollTo(Window window, PatientsViewModel patients, int index)
    {
        window.GetVisualDescendants().OfType<DataGrid>().First(g => g.IsEffectivelyVisible).ScrollIntoView(patients.Items[index], null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Numbers_follow_the_page_after_flips_refreshes_and_searches()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var requested = 1;
        app.Api.On(HttpMethod.Get, PatientsPath, request =>
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            requested = int.Parse(query["page"] ?? "1", System.Globalization.CultureInfo.InvariantCulture);
            return FakeApiHandler.Json(new PagedResult<PatientDto>(Page(requested), requested, 40, 200));
        });
        app.Navigation.NavigateToHome();
        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        var window = app.Show(shell);
        window.Height = 3000;
        shell.SelectedMenuIndex = shell.MenuItems.ToList().FindIndex(p => p.PageType == typeof(PatientsViewModel));
        var patients = Assert.IsType<PatientsViewModel>(shell.CurrentPage);
        await WaitUntil(() => !patients.IsLoading);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.True(CheckNumbers(window, patients) >= 5);
        Assert.Equal(1, patients.FirstRowNumber);

        // Scrolling recycles the drawn rows: their numbers must follow their new items.
        ScrollTo(window, patients, 39);
        CheckNumbers(window, patients);
        ScrollTo(window, patients, 20);
        CheckNumbers(window, patients);

        patients.CurrentPage = 3;
        await WaitUntil(() => !patients.IsLoading);
        Assert.Equal((3, 81), (requested, patients.FirstRowNumber));
        CheckNumbers(window, patients);
        ScrollTo(window, patients, 39);
        CheckNumbers(window, patients);

        await patients.RefreshCommand.ExecuteAsync(null);
        CheckNumbers(window, patients);

        patients.CurrentPage = 1;
        await WaitUntil(() => !patients.IsLoading);
        Assert.Equal(1, patients.FirstRowNumber);
        CheckNumbers(window, patients);
        ScrollTo(window, patients, 30);
        CheckNumbers(window, patients);

        patients.Search = "Mona";
        await patients.SearchCommand.ExecuteAsync(null);
        CheckNumbers(window, patients);
    }
}

using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Create a lab test (no <paramref name="existing"/>) or edit one. Lab cost is typed in currency units.</summary>
public partial class LabTestFormViewModel : FormViewModel
{
    private readonly LabTestsApi _tests;
    private readonly LabTestDto? _existing;

    public LabTestFormViewModel(LabTestsApi tests, IDialogService dialogs, LocalizationService localization, LabTestDto? existing = null)
        : base(dialogs, localization)
    {
        _tests = tests;
        _existing = existing;

        if (existing is not null)
        {
            Code = existing.Code;
            Name = existing.Name;
            LabCost = Money.ToMajor(existing.LabCost);
            IsActive = existing.IsActive;
        }

        MarkClean();
    }

    public bool IsNew => _existing is null;

    /// <summary>Highest amount the input accepts, in currency units.</summary>
    public decimal MaxAmount { get; } = Money.ToMajor(LabTestRules.MaxAmount);

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal? LabCost { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    protected override object BuildRequest() => _existing is null
        ? new CreateLabTestRequest { Code = Code.Trim(), Name = Name.Trim(), LabCost = ToMinor(LabCost) }
        : new UpdateLabTestRequest
        {
            Code = Code.Trim(),
            Name = Name.Trim(),
            LabCost = ToMinor(LabCost),
            IsActive = IsActive,
            Version = _existing.Version,
        };

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken) => request switch
    {
        CreateLabTestRequest create => await _tests.CreateTestAsync(create, cancellationToken),
        _ => await _tests.UpdateTestAsync(_existing!.Id, (UpdateLabTestRequest)request, cancellationToken),
    };

    private static long? ToMinor(decimal? amount) => amount is { } value ? Money.ToMinor(value) : null;
}

using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Settings;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.Settings;

/// <summary>Clinic name, address, phone and currency. Everyone reads them; Admin edits them. Failures throw AppException.</summary>
public interface IClinicSettingsService
{
    Task<ClinicSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task<string> GetCurrencyCodeAsync(CancellationToken cancellationToken);

    Task<ClinicSettingsDto> UpdateAsync(UpdateClinicSettingsRequest request, CancellationToken cancellationToken);
}

public class ClinicSettingsService(
    IClinicSettingsRepository settings,
    SingleValueCache<ClinicSettingsDto> cache,
    IActivityLogger activity) : IClinicSettingsService
{
    public Task<ClinicSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        cache.GetAsync(cancellationToken);

    public async Task<string> GetCurrencyCodeAsync(CancellationToken cancellationToken) =>
        (await cache.GetAsync(cancellationToken)).CurrencyCode;

    public async Task<ClinicSettingsDto> UpdateAsync(UpdateClinicSettingsRequest request, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        if (current.Version != request.Version)
        {
            throw AppException.Conflict(ErrorCodes.ConcurrencyConflict);
        }

        current.Update(request.Name, request.Address, request.Phone, request.CurrencyCode);
        await settings.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.ClinicSettingsUpdated, ActivityModules.Settings, "Updated the clinic settings")
            {
                EntityType = nameof(ClinicSettings),
                EntityId = current.Id.ToString(),
                Details = new { current.Name, current.CurrencyCode },
            },
            cancellationToken);

        return ToDto(current);
    }

    private static ClinicSettingsDto ToDto(ClinicSettings s) =>
        new(s.Name, s.Address, s.Phone, s.CurrencyCode, s.Version, s.UpdatedAt, s.UpdatedBy);
}

using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Settings;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.Settings;

/// <summary>Backup folder and maintenance mode. Super reads and edits them. Failures throw AppException.</summary>
public interface ISystemSettingsService
{
    Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task<SystemSettingsDto> UpdateAsync(UpdateSystemSettingsRequest request, CancellationToken cancellationToken);
}

public class SystemSettingsService(ISystemSettingsRepository settings, IActivityLogger activity) : ISystemSettingsService
{
    public async Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await settings.GetAsync(cancellationToken));

    public async Task<SystemSettingsDto> UpdateAsync(UpdateSystemSettingsRequest request, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        if (current.Version != request.Version)
        {
            throw AppException.Conflict(ErrorCodes.ConcurrencyConflict);
        }

        current.Update(request.BackupPath, request.MaintenanceMode);
        await settings.SaveChangesAsync(cancellationToken);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.SystemSettingsUpdated, ActivityModules.Settings, "Updated the system settings")
            {
                EntityType = nameof(SystemSettings),
                EntityId = current.Id.ToString(),
                Details = new { current.BackupPath, current.MaintenanceMode },
            },
            cancellationToken);

        return ToDto(current);
    }

    private static SystemSettingsDto ToDto(SystemSettings s) =>
        new(s.BackupPath, s.MaintenanceMode, s.Version, s.UpdatedAt, s.UpdatedBy);
}

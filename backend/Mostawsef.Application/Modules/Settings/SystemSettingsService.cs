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

public class SystemSettingsService(
    ISystemSettingsRepository settings,
    ISystemSettingsCache cache,
    IActivityLogger activity) : ISystemSettingsService
{
    public Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        cache.GetAsync(cancellationToken);

    public async Task<SystemSettingsDto> UpdateAsync(UpdateSystemSettingsRequest request, CancellationToken cancellationToken)
    {
        var current = await settings.GetForUpdateAsync(cancellationToken);
        if (current.Version != request.Version)
        {
            throw AppException.Conflict(ErrorCodes.ConcurrencyConflict);
        }

        current.Update(request.BackupPath, request.MaintenanceMode);
        await settings.SaveChangesAsync(cancellationToken);
        cache.Invalidate();

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.SystemSettingsUpdated, ActivityModules.Settings, "Updated the system settings")
            {
                EntityType = nameof(SystemSettings),
                EntityId = current.Id.ToString(),
                Details = new { current.BackupPath, current.MaintenanceMode },
            },
            cancellationToken);

        return new SystemSettingsDto(current.BackupPath, current.MaintenanceMode, current.Version, current.UpdatedAt, current.UpdatedBy);
    }
}

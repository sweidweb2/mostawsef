using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Auth;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Application.Modules.LabTests;
using Mostawsef.Application.Modules.Maintenance;
using Mostawsef.Application.Modules.Logs;
using Mostawsef.Application.Modules.Orders;
using Mostawsef.Application.Modules.Patients;
using Mostawsef.Application.Modules.Settings;
using Mostawsef.Application.Modules.Users;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddScoped<ISystemErrorService, SystemErrorService>();
        services.AddScoped<ILogRetentionService, LogRetentionService>();
        // One cached row each (singleton); the owning service invalidates it after its own writes.
        services.AddSingleton(sp => new SingleValueCache<ClinicSettingsDto>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            (provider, ct) => provider.GetRequiredService<IClinicSettingsRepository>().GetSnapshotAsync(ct)));
        services.AddSingleton(sp => new SingleValueCache<SystemSettingsDto>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            (provider, ct) => provider.GetRequiredService<ISystemSettingsRepository>().GetSnapshotAsync(ct)));
        services.AddScoped<IClinicSettingsService, ClinicSettingsService>();
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<ILabTestService, LabTestService>();
        services.AddScoped<IPatientService, PatientService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IScheduledBackupService, ScheduledBackupService>();
        services.AddScoped<ISystemMaintenanceService, SystemMaintenanceService>();

        return services;
    }
}

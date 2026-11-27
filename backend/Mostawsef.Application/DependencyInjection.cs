using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Modules.Auth;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Application.Modules.LabTests;
using Mostawsef.Application.Modules.Maintenance;
using Mostawsef.Application.Modules.Logs;
using Mostawsef.Application.Modules.Orders;
using Mostawsef.Application.Modules.Patients;
using Mostawsef.Application.Modules.Settings;
using Mostawsef.Application.Modules.Users;

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
        services.AddSingleton<ClinicSettingsCache>();
        services.AddSingleton<SystemSettingsCache>();
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

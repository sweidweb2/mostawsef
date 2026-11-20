using System.Text.Json;
using System.Text.Json.Serialization;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Logs;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Orders;
using Mostawsef.Contracts.Patients;
using Mostawsef.Contracts.Settings;
using Mostawsef.Contracts.Users;

namespace Mostawsef.Contracts.Common;

/// <summary>
/// Compile-time JSON metadata for every request and response of the API, shared by the API and the desktop app.
/// Without it, System.Text.Json builds this metadata with reflection the first time each type is sent or read,
/// which slows the first call of every endpoint. Both sides put it first in their resolver chain and keep the
/// reflection resolver behind it, so a type missing here still works (just without the head start).
/// Add new request/response types (and each closed PagedResult/CursorPage/list) here.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HealthDto))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(ChangePasswordRequest))]
[JsonSerializable(typeof(UserDto))]
[JsonSerializable(typeof(UserDetailsDto))]
[JsonSerializable(typeof(CreateUserRequest))]
[JsonSerializable(typeof(UpdateUserRequest))]
[JsonSerializable(typeof(ResetPasswordRequest))]
[JsonSerializable(typeof(PagedResult<UserListItemDto>))]
[JsonSerializable(typeof(ClinicSettingsDto))]
[JsonSerializable(typeof(UpdateClinicSettingsRequest))]
[JsonSerializable(typeof(SystemSettingsDto))]
[JsonSerializable(typeof(UpdateSystemSettingsRequest))]
[JsonSerializable(typeof(LabTestDto))]
[JsonSerializable(typeof(CreateLabTestRequest))]
[JsonSerializable(typeof(UpdateLabTestRequest))]
[JsonSerializable(typeof(PagedResult<LabTestDto>))]
[JsonSerializable(typeof(IReadOnlyList<LabTestDto>))]
[JsonSerializable(typeof(PatientDto))]
[JsonSerializable(typeof(CreatePatientRequest))]
[JsonSerializable(typeof(UpdatePatientRequest))]
[JsonSerializable(typeof(PagedResult<PatientDto>))]
[JsonSerializable(typeof(IReadOnlyList<PatientDto>))]
[JsonSerializable(typeof(OrderDetailsDto))]
[JsonSerializable(typeof(OrderDashboardDto))]
[JsonSerializable(typeof(CreateOrderRequest))]
[JsonSerializable(typeof(AddPaymentRequest))]
[JsonSerializable(typeof(UpdatePaymentRequest))]
[JsonSerializable(typeof(OrderListFilterRequest))]
[JsonSerializable(typeof(PagedResult<OrderListItemDto>))]
[JsonSerializable(typeof(ActivityLogDto))]
[JsonSerializable(typeof(ActivityLogStatsDto))]
[JsonSerializable(typeof(CursorPage<ActivityLogListItemDto>))]
[JsonSerializable(typeof(SystemErrorDto))]
[JsonSerializable(typeof(SystemErrorStatsDto))]
[JsonSerializable(typeof(CursorPage<SystemErrorListItemDto>))]
[JsonSerializable(typeof(ReportClientErrorRequest))]
[JsonSerializable(typeof(DeleteLogsRequest))]
[JsonSerializable(typeof(DeleteLogsResultDto))]
[JsonSerializable(typeof(ResetDataRequest))]
[JsonSerializable(typeof(ResetDataResultDto))]
[JsonSerializable(typeof(SuperDashboardDto))]
[JsonSerializable(typeof(BackupRecordDto))]
[JsonSerializable(typeof(CreateBackupRequest))]
[JsonSerializable(typeof(PagedResult<BackupRecordDto>))]
[JsonSerializable(typeof(ScheduledDumpDto))]
[JsonSerializable(typeof(PagedResult<ScheduledDumpDto>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
public sealed partial class ContractsJsonContext : JsonSerializerContext;

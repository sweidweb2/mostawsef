using System;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>User management (Super only).</summary>
public class UsersApi(ApiClient api)
{
    public Task<ApiResult<PagedResult<UserListItemDto>>> GetUsersAsync(
        int page, int pageSize, string? search, bool includeTotal = true, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<UserListItemDto>>(
            $"api/users?page={page}&pageSize={pageSize}&includeTotal={includeTotal.ToString().ToLowerInvariant()}" +
            (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}"),
            cancellationToken);

    public Task<ApiResult<UserDetailsDto>> GetUserAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<UserDetailsDto>($"api/users/{id}", cancellationToken);

    public Task<ApiResult<UserDetailsDto>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreateUserRequest, UserDetailsDto>("api/users", request, cancellationToken);

    public Task<ApiResult<UserDetailsDto>> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdateUserRequest, UserDetailsDto>($"api/users/{id}", request, cancellationToken);

    public Task<ApiResult<bool>> ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync($"api/users/{id}/reset-password", request, cancellationToken);

    public Task<ApiResult<bool>> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/users/{id}", cancellationToken);
}

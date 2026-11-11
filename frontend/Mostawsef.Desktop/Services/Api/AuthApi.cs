using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Desktop.Services.Api;

public class AuthApi(ApiClient api)
{
    public Task<ApiResult<LoginResponse>> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
        api.PostAsync<LoginRequest, LoginResponse>("api/auth/login", new LoginRequest(username, password), cancellationToken);

    public Task<ApiResult<UserDto>> MeAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<UserDto>("api/auth/me", cancellationToken);

    public Task<ApiResult<LoginResponse>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<ChangePasswordRequest, LoginResponse>("api/auth/change-password", request, cancellationToken);

    /// <summary>Records the sign-out on the server (activity log). The session is cleared by SessionService.</summary>
    public Task<ApiResult<bool>> LogoutAsync(CancellationToken cancellationToken = default) =>
        api.PostAsync("api/auth/logout", new { }, cancellationToken);
}

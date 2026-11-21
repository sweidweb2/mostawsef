using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Desktop.Services.Api;

public class AuthApi(ApiClient api)
{
    public Task<ApiResult<LoginResponse>> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
        api.PostAsync<LoginRequest, LoginResponse>("api/auth/login", new LoginRequest(username, password), cancellationToken);

    /// <summary>Whether the login screen may open the Admin portal without a password.</summary>
    public Task<ApiResult<ClinicAccessResponse>> GetClinicAccessAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<ClinicAccessResponse>("api/auth/clinic-access", cancellationToken);

    /// <summary>Opens the Admin portal as the built-in clinic identity (no password).</summary>
    public Task<ApiResult<LoginResponse>> OpenClinicSessionAsync(CancellationToken cancellationToken = default) =>
        api.PostAsync<object, LoginResponse>("api/auth/clinic-session", new { }, cancellationToken);

    public Task<ApiResult<LoginResponse>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<ChangePasswordRequest, LoginResponse>("api/auth/change-password", request, cancellationToken);

    /// <summary>Records the sign-out on the server (activity log). The session is cleared by SessionService.</summary>
    public Task<ApiResult<bool>> LogoutAsync(CancellationToken cancellationToken = default) =>
        api.PostAsync("api/auth/logout", new { }, cancellationToken);
}

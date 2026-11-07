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
}

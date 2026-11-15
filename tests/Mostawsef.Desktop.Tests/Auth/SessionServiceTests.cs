using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Auth;

[Collection(UiTestCollection.Name)]
public class SessionServiceTests : UiTestBase
{
    [AvaloniaFact]
    public void A_session_stays_valid_when_the_pc_clock_is_ahead_of_the_server()
    {
        var session = new SessionService();
        // Absolute ExpiresAt is already two days ago on this PC, but the server sent an 8-hour lifetime.
        session.SignIn(new LoginResponse(
            "tok",
            DateTime.UtcNow.AddDays(-2),
            new UserDto(Guid.NewGuid(), "admin@test.local", Roles.Admin),
            ExpiresInSeconds: 8 * 3600));

        Assert.True(session.IsAuthenticated);
        Assert.Equal("tok", session.AccessToken);
    }

    [AvaloniaFact]
    public void Without_a_lifetime_a_past_ExpiresAt_is_already_expired()
    {
        var session = new SessionService();
        session.SignIn(new LoginResponse(
            "tok",
            DateTime.UtcNow.AddDays(-2),
            new UserDto(Guid.NewGuid(), "admin@test.local", Roles.Admin)));

        Assert.False(session.IsAuthenticated);
        Assert.Null(session.AccessToken);
    }
}

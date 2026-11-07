using Microsoft.AspNetCore.Mvc;
using Mostawsef.Contracts.Common;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var databaseReachable = await dbContext.Database.CanConnectAsync(cancellationToken);

        return databaseReachable
            ? Ok(new HealthDto("Healthy", "Connected", ApiVersion.Current))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthDto("Unhealthy", "Unreachable", ApiVersion.Current));
    }
}

using Microsoft.AspNetCore.Mvc;
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
            ? Ok(new { status = "Healthy", database = "Connected" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", database = "Unreachable" });
    }
}

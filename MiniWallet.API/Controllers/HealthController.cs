using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniWallet.Infrastructure;

namespace MiniWallet.API.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(WalletDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "healthy" });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            if (await db.Database.CanConnectAsync(cancellationToken)
                && !(await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                return Ok(new { status = "ready" });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Database readiness failed");
        }
        return Problem(statusCode: 503, title: "Database unavailable");
    }
}


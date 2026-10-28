using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniWallet.Infrastructure;

namespace MiniWallet.API.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(WalletDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    /// <summary>Reports that the HTTP host is running.</summary>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">Host is alive; database state is not checked.</response>
    /// <remarks>Anonymous probe; no authentication required.</remarks>
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "healthy" });

    /// <summary>Checks database connectivity and whether all migrations are applied.</summary>
    /// <param name="cancellationToken">Probe cancellation token.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">Database connects and has no pending migrations.</response>
    /// <response code="503">Database unavailable or migrations pending; retry the probe.</response>
    /// <remarks>Anonymous probe; does not run migrations or guarantee future request success.</remarks>
    [HttpGet("ready")]
    [ProducesResponseType<ProblemDetails>(503)]
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


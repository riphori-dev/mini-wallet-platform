using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.Application;

namespace MiniWallet.API.Controllers;

[ApiController]
[Authorize(Policy = WalletAuthorization.InternalProvisioning)]
public sealed class PlayerRegistrationsController(ProvisionPlayer provision) : ControllerBase
{
    /// <summary>Accepts an internal registration event and atomically provisions zero USD/EUR/BTC wallets.</summary>
    /// <param name="registration">Validated version-1 event from the authenticated internal sender.</param>
    /// <param name="ct">Cancels request processing; cancellation does not establish whether a write committed.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="204">Receipt and wallets committed, or the same event was already processed.</response>
    /// <response code="400">Invalid event; not accepted.</response>
    /// <response code="401">Missing or invalid internal-audience JWT.</response>
    /// <response code="403">Missing internal machine role or players:provision scope.</response>
    /// <response code="409">Event ID was reused with a different registration payload.</response>
    /// <response code="503">Transient failure; retry the same event ID and payload.</response>
    /// <remarks>Requires wallet-internal audience, kind=machine, role=internal and players:provision scope. Player/provider tokens do not authorize delivery. Inbox and wallets commit together; duplicate delivery is safe. Retry unchanged after timeout or lost acknowledgement. Errors use Problem Details with code and traceId.</remarks>
    [HttpPost("/internal/v1/player-registrations")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Receive(PlayerRegistered registration, CancellationToken ct)
    {
        await provision.ExecuteAsync(registration, ct);
        return NoContent();
    }
}


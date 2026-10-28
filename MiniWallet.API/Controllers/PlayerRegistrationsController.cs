using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.Application;

namespace MiniWallet.API.Controllers;

[ApiController]
[Authorize(Policy = WalletAuthorization.InternalProvisioning)]
public sealed class PlayerRegistrationsController(ProvisionPlayer provision) : ControllerBase
{
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


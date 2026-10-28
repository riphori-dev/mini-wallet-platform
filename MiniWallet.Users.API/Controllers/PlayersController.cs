using Microsoft.AspNetCore.Mvc;
using MiniWallet.Users.Application;

namespace MiniWallet.Users.API.Controllers;

[ApiController]
public sealed class PlayersController(UserUseCases users) : ControllerBase
{
    /// <summary>Register a player and queue automatic wallet provisioning.</summary>
    [HttpPost("/api/v1/players")]
    [ProducesResponseType<RegistrationResult>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(409)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) =>
        StatusCode(201, await users.RegisterAsync(request, ct));

    [HttpPost("/api/v1/email-verifications")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<IActionResult> Verify(VerifyRequest request, CancellationToken ct)
    {
        await users.VerifyAsync(request, ct);
        return NoContent();
    }

    /// <summary>Authenticate before replacing a verification token. Previous tokens become invalid.</summary>
    [HttpPost("/api/v1/email-verifications/resend")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Resend(CredentialsRequest request, CancellationToken ct)
    {
        await users.ResendAsync(request, ct);
        return NoContent();
    }
}


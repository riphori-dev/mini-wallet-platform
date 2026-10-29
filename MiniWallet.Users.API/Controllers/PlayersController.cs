using Microsoft.AspNetCore.Mvc;
using MiniWallet.Users.Application;

namespace MiniWallet.Users.API.Controllers;

[ApiController]
public sealed class PlayersController(UserUseCases users) : ControllerBase
{
    /// <summary>Registers an unverified player and queues automatic wallet provisioning.</summary>
    /// <param name="request">Email and password; caller cannot select identity or role.</param>
    /// <param name="ct">Cancels request processing; cancellation does not establish whether a write committed.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="201">User and registration outbox record committed; provisioningStatus is pending.</response>
    /// <response code="400">Invalid email/password or JSON.</response>
    /// <response code="409">Normalized email already registered; no second player created.</response>
    /// <response code="503">Transient database failure; retry registration or recover using the same credentials.</response>
    /// <remarks>Anonymous endpoint. Email delivery follows commit and may be unavailable without rejecting registration. No registration replay framework: after a lost response retry the same email; a duplicate 409 is recovered through login or verification resend. Wallet outages leave durable pending provisioning. Errors use Problem Details with code and traceId.</remarks>
    [HttpPost("/api/v1/register")]
    [ProducesResponseType<RegistrationResult>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await users.RegisterAsync(request, ct);
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "TargetPlayerId", result.PlayerId.ToString());
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "VerificationDeliveryStatus", result.VerificationDeliveryStatus);
        return StatusCode(201, result);
    }

    /// <summary>Consumes an email verification token.</summary>
    /// <param name="request">64-character hexadecimal token from the verification message.</param>
    /// <param name="ct">Cancels request processing; cancellation does not establish whether a write committed.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="204">Verification committed.</response>
    /// <response code="400">Malformed, invalid, expired or already consumed token.</response>
    /// <response code="503">Transient database failure.</response>
    /// <remarks>Anonymous possession-of-token endpoint. Token expires after 24 hours and can be consumed once. A retry after successful consumption returns 400; it does not undo verification. After an uncertain response, try login or resend. Errors use Problem Details with code and traceId.</remarks>
    [HttpPost("/api/v1/email-verifications")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Verify(VerifyRequest request, CancellationToken ct)
    {
        await users.VerifyAsync(request, ct);
        return NoContent();
    }

    /// <summary>Authenticates credentials and replaces an unverified user’s verification token.</summary>
    /// <param name="request">Registered email and password; no bearer token is required.</param>
    /// <param name="ct">Cancels request processing; cancellation does not establish whether a write committed.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="204">Replacement message sent, or the account was already verified.</response>
    /// <response code="400">Invalid credential format.</response>
    /// <response code="401">Incorrect credentials.</response>
    /// <response code="503">Transient database or delivery failure; retry with the same credentials.</response>
    /// <remarks>Previously issued tokens become invalid when replacement commits. Sending occurs after replacement; delivery failure is recovered by resending again. Already verified accounts return 204. Errors use Problem Details with code and traceId.</remarks>
    [HttpPost("/api/v1/email-verifications/resend")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Resend(CredentialsRequest request, CancellationToken ct)
    {
        await users.ResendAsync(request, ct);
        return NoContent();
    }
}


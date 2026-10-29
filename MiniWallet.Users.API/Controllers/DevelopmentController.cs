using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.Users.Application;
using MiniWallet.Users.Infrastructure;

namespace MiniWallet.Users.API.Controllers;

[ApiController]
[Route("dev")]
public sealed class DevelopmentController(IWebHostEnvironment environment, UserUseCases users,
    IServiceProvider services, IConfiguration configuration) : ControllerBase
{
    /// <summary>Issues a short-lived Development user JWT after password and email-verification checks.</summary>
    /// <param name="request">User credentials; subject and role come from persisted identity.</param>
    /// <param name="ct">Cancels request processing; cancellation does not establish whether a write committed.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">Bearer JWT expiring in 600 seconds; response is not cached.</response>
    /// <response code="400">Invalid credential format.</response>
    /// <response code="401">Incorrect credentials.</response>
    /// <response code="403">Email is not verified.</response>
    /// <response code="404">Endpoint unavailable outside Development.</response>
    /// <response code="503">Transient database failure; retry login.</response>
    /// <remarks>No incoming bearer token required. Player registration cannot grant admin/provider identity. Login returns the stored player or seeded admin role; providers use the separate machine flow. Errors use Problem Details with code and traceId.</remarks>
    [HttpPost("token")]
    [ProducesResponseType<AccessToken>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Login(CredentialsRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var identity = await users.LoginAsync(request, ct);
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "ActorId", identity.Id.ToString());
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "Role", identity.Role);
        Response.Headers.CacheControl = "no-store";
        return Ok(services.GetRequiredService<IAccessTokenIssuer>().ForUser(identity));
    }

    /// <summary>Issues a Development JWT from distinct configured machine credentials.</summary>
    /// <param name="request">Client credentials and requested allowed scope; neither client ID nor scope is trusted without authentication.</param>
    /// <param name="ct">Validation cancellation token.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">Bearer JWT expiring in 600 seconds.</response>
    /// <response code="400">Invalid fields or unsupported scope.</response>
    /// <response code="401">Incorrect client credentials.</response>
    /// <response code="403">Scope not allowed for this client.</response>
    /// <response code="404">Endpoint unavailable outside Development.</response>
    /// <remarks>JSON development issuer, not a production OAuth endpoint. Provider credentials grant transactions:write with wallet-api audience. Separate internal credentials grant players:provision with wallet-internal audience. No player login or arbitrary role/subject selection. Repeat credential requests issue new tokens; no transaction key is reserved. Errors use Problem Details with code and traceId.</remarks>
    [HttpPost("connect/token")]
    [ProducesResponseType<AccessToken>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<IActionResult> Machine(MachineTokenRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var result = await services.GetRequiredService<IValidator<MachineTokenRequest>>().ValidateAsync(request, ct);
        if (!result.IsValid) throw new UseCaseFailure(400, "invalid_request", "Invalid machine token request.");
        Response.Headers.CacheControl = "no-store";
        var token = services.GetRequiredService<IAccessTokenIssuer>().ForMachine(request.ClientId, request.ClientSecret, request.Scope);
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "ActorId", request.ClientId);
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "Role", "machine");
        return Ok(token);
    }

    /// <summary>Reads recent plaintext verification messages from the local Development mailbox.</summary>
    /// <param name="key">X-Dev-Mailbox-Key header matching Development configuration.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">At most 100 in-memory verification messages, oldest first.</response>
    /// <response code="401">Missing or incorrect mailbox key.</response>
    /// <response code="404">Endpoint unavailable outside Development.</response>
    /// <remarks>Requires the local mailbox key, not a JWT. Messages are sensitive, not cached, and lost on Users restart; resend with account credentials to recover.</remarks>
    [HttpGet("mailbox")]
    [ProducesResponseType<VerificationMessage[]>(200)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(404)]
    public IActionResult Mailbox([FromHeader(Name = "X-Dev-Mailbox-Key")] string? key)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (key is null || !DevelopmentTokenIssuer.SameSecret(key, configuration["Development:MailboxKey"]!))
            return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(services.GetRequiredService<DevelopmentMailbox>().Read());
    }
}


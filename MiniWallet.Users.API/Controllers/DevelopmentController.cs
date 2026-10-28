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
    [HttpPost("token")]
    [ProducesResponseType<AccessToken>(200)]
    public async Task<IActionResult> Login(CredentialsRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var identity = await users.LoginAsync(request, ct);
        Response.Headers.CacheControl = "no-store";
        return Ok(services.GetRequiredService<IAccessTokenIssuer>().ForUser(identity));
    }

    /// <summary>Development JSON client credentials, not a production OAuth token endpoint.</summary>
    [HttpPost("connect/token")]
    [ProducesResponseType<AccessToken>(200)]
    public async Task<IActionResult> Machine(MachineTokenRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var result = await services.GetRequiredService<IValidator<MachineTokenRequest>>().ValidateAsync(request, ct);
        if (!result.IsValid) throw new UseCaseFailure(400, "invalid_request", "Invalid machine token request.");
        Response.Headers.CacheControl = "no-store";
        return Ok(services.GetRequiredService<IAccessTokenIssuer>().ForMachine(request.ClientId, request.ClientSecret, request.Scope));
    }

    /// <summary>Local reviewer mailbox. Send X-Dev-Mailbox-Key from Development configuration.</summary>
    [HttpGet("mailbox")]
    public IActionResult Mailbox([FromHeader(Name = "X-Dev-Mailbox-Key")] string? key)
    {
        if (!environment.IsDevelopment()) return NotFound();
        if (key is null || !DevelopmentTokenIssuer.SameSecret(key, configuration["Development:MailboxKey"]!))
            return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(services.GetRequiredService<DevelopmentMailbox>().Read());
    }
}


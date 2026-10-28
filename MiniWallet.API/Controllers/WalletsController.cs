using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.Application;

namespace MiniWallet.API.Controllers;

[ApiController]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class WalletsController(ReadWallets reads) : ControllerBase
{
    [Authorize(Policy = WalletAuthorization.PlayerRead)]
    [HttpGet("/api/v1/me/wallets")]
    public Task<PlayerBalances> Mine(CancellationToken ct) => reads.BalancesAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct);

    [Authorize(Policy = WalletAuthorization.PlayerRead)]
    [HttpGet("/api/v1/me/wallets/{currency}/transactions")]
    public Task<HistoryPage> MyHistory(string currency, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        reads.HistoryAsync(new(Guid.Parse(User.FindFirst("sub")!.Value), currency, page, pageSize), ct);

    [Authorize(Policy = WalletAuthorization.AdminRead)]
    [HttpGet("/api/v1/players/{playerId:guid}/wallets")]
    public Task<PlayerBalances> Any(Guid playerId, CancellationToken ct) => reads.BalancesAsync(playerId, ct);

    [Authorize(Policy = WalletAuthorization.AdminRead)]
    [HttpGet("/api/v1/players/{playerId:guid}/wallets/{currency}/transactions")]
    public Task<HistoryPage> AnyHistory(Guid playerId, string currency, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => reads.HistoryAsync(new(playerId, currency, page, pageSize), ct);
}


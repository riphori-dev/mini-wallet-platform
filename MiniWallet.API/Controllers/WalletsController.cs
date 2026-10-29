using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.API.Wallet;
using MiniWallet.Application.Services;

namespace MiniWallet.API.Controllers;

[ApiController]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class WalletsController(ReadWallets reads) : ControllerBase
{
    /// <summary>Reads balances for the authenticated player.</summary>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">The requested balances or history page.</response>
    /// <response code="400">Invalid owner or history query.</response>
    /// <response code="401">Missing, invalid or expired JWT.</response>
    /// <response code="403">JWT lacks the required role, kind or scope.</response>
    /// <response code="404">Wallet is unavailable or not yet provisioned.</response>
    /// <response code="503">Transient database failure; retry the read.</response>
    /// <remarks>Requires wallet-api audience, kind=user, role=player, email_verified=true, wallet:read scope and a GUID sub. Ownership comes only from sub. Providers cannot use this route. History orders by createdAt then internal transaction ID, both descending; new writes can shift offset pages.</remarks>
    [Authorize(Policy = WalletAuthorization.PlayerRead)]
    [HttpGet("/api/v1/me/wallets")]
    [ProducesResponseType<PlayerBalances>(200)]
    public Task<PlayerBalances> Mine(CancellationToken ct) => reads.BalancesAsync(Guid.Parse(User.FindFirst("sub")!.Value), ct);

    /// <summary>Reads accepted history for the authenticated player wallet.</summary>
    /// <param name="currency">Case-sensitive USD, EUR or BTC.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <param name="page">One-based page, 1–10000; default 1.</param>
    /// <param name="pageSize">Entries per page, 1–100; default 20.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">The requested balances or history page.</response>
    /// <response code="400">Invalid owner or history query.</response>
    /// <response code="401">Missing, invalid or expired JWT.</response>
    /// <response code="403">JWT lacks the required role, kind or scope.</response>
    /// <response code="404">Wallet is unavailable or not yet provisioned.</response>
    /// <response code="503">Transient database failure; retry the read.</response>
    /// <remarks>Requires verified player JWT and wallet:read scope. Ownership comes only from sub. Providers cannot use this route. History orders by createdAt then internal transaction ID, both descending; new writes can shift offset pages.</remarks>
    [Authorize(Policy = WalletAuthorization.PlayerRead)]
    [HttpGet("/api/v1/me/wallets/{currency}/transactions")]
    [ProducesResponseType<HistoryPage>(200)]
    public Task<HistoryPage> MyHistory(string currency, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        reads.HistoryAsync(new(Guid.Parse(User.FindFirst("sub")!.Value), currency, page, pageSize), ct);

    /// <summary>Reads balances for a player selected by an admin.</summary>
    /// <param name="playerId">Target owner GUID, not caller identity.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">The requested balances or history page.</response>
    /// <response code="400">Invalid owner or history query.</response>
    /// <response code="401">Missing, invalid or expired JWT.</response>
    /// <response code="403">JWT lacks the required role, kind or scope.</response>
    /// <response code="404">Wallet is unavailable or not yet provisioned.</response>
    /// <response code="503">Transient database failure; retry the read.</response>
    /// <remarks>Requires wallet-api user JWT with admin role and wallet:read:any scope. Providers cannot use this route. History orders by createdAt then internal transaction ID, both descending; new writes can shift offset pages.</remarks>
    [Authorize(Policy = WalletAuthorization.AdminRead)]
    [HttpGet("/api/v1/players/{playerId:guid}/wallets")]
    [ProducesResponseType<PlayerBalances>(200)]
    public Task<PlayerBalances> Any(Guid playerId, CancellationToken ct) => reads.BalancesAsync(playerId, ct);

    /// <summary>Reads accepted history for a player wallet selected by an admin.</summary>
    /// <param name="playerId">Target owner GUID, not caller identity.</param>
    /// <param name="currency">Case-sensitive USD, EUR or BTC.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <param name="page">One-based page, 1–10000; default 1.</param>
    /// <param name="pageSize">Entries per page, 1–100; default 20.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="200">The requested balances or history page.</response>
    /// <response code="400">Invalid owner or history query.</response>
    /// <response code="401">Missing, invalid or expired JWT.</response>
    /// <response code="403">JWT lacks the required role, kind or scope.</response>
    /// <response code="404">Wallet is unavailable or not yet provisioned.</response>
    /// <response code="503">Transient database failure; retry the read.</response>
    /// <remarks>Requires wallet-api user JWT with admin role and wallet:read:any scope. Providers cannot use this route. History orders by createdAt then internal transaction ID, both descending; new writes can shift offset pages.</remarks>
    [Authorize(Policy = WalletAuthorization.AdminRead)]
    [HttpGet("/api/v1/players/{playerId:guid}/wallets/{currency}/transactions")]
    [ProducesResponseType<HistoryPage>(200)]
    public Task<HistoryPage> AnyHistory(Guid playerId, string currency, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => reads.HistoryAsync(new(playerId, currency, page, pageSize), ct);
}


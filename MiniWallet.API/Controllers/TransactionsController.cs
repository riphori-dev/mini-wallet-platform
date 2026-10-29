using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.API.Wallet;
using MiniWallet.Application;

namespace MiniWallet.API.Controllers;

[ApiController]
[Authorize(Policy = WalletAuthorization.ProviderWrite)]
public sealed class TransactionsController(SubmitTransaction submit) : ControllerBase
{
    /// <summary>Submits a provider BET debit or a WIN payout against an accepted BET.</summary>
    /// <param name="request">Provider payload; provider identity is derived from validated provider_id claims, never this body.</param>
    /// <param name="ct">Cancels request processing; cancellation does not establish whether a write committed.</param>
    /// <returns>The HTTP result described by the response codes.</returns>
    /// <response code="201">Committed ledger entry and original post-operation balance; exact replay returns the stored response.</response>
    /// <response code="400">Malformed or invalid input; no key reserved.</response>
    /// <response code="401">Missing, invalid or expired JWT; no key reserved.</response>
    /// <response code="403">Requires provider machine identity and transactions:write scope; no key reserved.</response>
    /// <response code="404">Wallet unavailable; retry the same ID after provisioning.</response>
    /// <response code="409">Changed payload for the same key, or terminal financial rejection.</response>
    /// <response code="503">Transient database/lock failure; retry with the same ID and typed payload.</response>
    /// <remarks>Requires wallet-api JWT, kind=machine, role=provider and transactions:write scope. Key=(provider_id,externalTransactionId), across all wallets. Exact typed replay returns original status/body, including balanceAfter; changed payload conflicts. Terminal financial rejections reserve the key. Validation/auth failures and missing wallets do not. A timeout or lost response requires the same key and typed payload; never infer success before commit. WIN requires the matching provider/player/currency BET and allows one settlement. Errors are Problem Details with code and traceId; unexpected failures return sanitized 500 and also require same-key retry.</remarks>
    [HttpPost("/api/v1/transactions")]
    [ProducesResponseType<TransactionResponse>(201)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(401)]
    [ProducesResponseType<ProblemDetails>(403)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Execute(TransactionRequest request, CancellationToken ct)
    {
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "TargetPlayerId", request.PlayerId.ToString());
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "ExternalTransactionId", request.ExternalTransactionId);
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "TransactionType", request.Type);
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "Currency", request.Currency);
        var result = await submit.ExecuteAsync(User.FindFirst("provider_id")!.Value, request,
            Activity.Current?.Id ?? HttpContext.TraceIdentifier, ct);
        MiniWallet.Auditing.AuditDetails.Set(HttpContext, "IsReplay", result.IsReplay.ToString());
        if (result.Status == 409)
        {
            using var problem = System.Text.Json.JsonDocument.Parse(result.Body);
            MiniWallet.Auditing.AuditDetails.Set(HttpContext, "ErrorCode", problem.RootElement.GetProperty("code").GetString());
        }
        return new ContentResult { StatusCode = result.Status, Content = result.Body, ContentType = result.ContentType };
    }
}


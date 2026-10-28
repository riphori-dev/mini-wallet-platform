using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.Application;

namespace MiniWallet.API.Controllers;

[ApiController]
[Authorize(Policy = WalletAuthorization.ProviderWrite)]
public sealed class TransactionsController(SubmitTransaction submit) : ControllerBase
{
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
        var result = await submit.ExecuteAsync(User.FindFirst("provider_id")!.Value, request,
            Activity.Current?.Id ?? HttpContext.TraceIdentifier, ct);
        return new ContentResult { StatusCode = result.Status, Content = result.Body, ContentType = result.ContentType };
    }
}


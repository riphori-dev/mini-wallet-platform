using FluentValidation;

namespace MiniWallet.Application;

/// <summary>A wallet balance in one denomination.</summary>
/// <param name="Currency">USD, EUR or BTC.</param>
/// <param name="Balance">Nonnegative invariant fixed-scale decimal string: two fractional digits for USD/EUR and eight for BTC.</param>
public sealed record WalletBalance(string Currency, string Balance);
/// <summary>Provisioned balances for one player.</summary>
/// <param name="PlayerId">The wallet owner GUID selected by trusted subject or authorized admin route.</param>
/// <param name="Wallets">Wallet balances ordered by currency code; normal provisioning creates USD, EUR and BTC.</param>
public sealed record PlayerBalances(Guid PlayerId, IReadOnlyList<WalletBalance> Wallets);
/// <summary>A validated offset-page query for one player wallet.</summary>
/// <param name="PlayerId">Nonempty target owner GUID; authorization is performed by the endpoint.</param>
/// <param name="Currency">Case-sensitive USD, EUR or BTC.</param>
/// <param name="Page">One-based page, default 1; allowed range 1–10000.</param>
/// <param name="PageSize">Page size, default 20; allowed range 1–100.</param>
public sealed record HistoryRequest(Guid PlayerId, string Currency, int Page = 1, int PageSize = 20);
/// <summary>An accepted ledger operation; rejected transactions are excluded.</summary>
/// <param name="TransactionId">Internal ledger GUID, distinct from the external provider reference.</param>
/// <param name="ExternalTransactionId">Provider reference, or fixture-generated reference for DEV_FUNDING.</param>
/// <param name="Type">BET, WIN or DEV_FUNDING.</param>
/// <param name="Amount">Positive invariant fixed-scale amount in the queried wallet currency.</param>
/// <param name="BalanceAfter">Historical post-operation balance as an invariant fixed-scale string.</param>
/// <param name="CreatedAt">UTC ISO 8601 operation timestamp; history orders descending by timestamp then internal ID.</param>
/// <param name="OriginalBetId">Internal ledger GUID of the BET settled by a WIN; null otherwise. This is not a provider reference.</param>
public sealed record HistoryItem(Guid TransactionId, string ExternalTransactionId, string Type,
    string Amount, string BalanceAfter, DateTimeOffset CreatedAt, Guid? OriginalBetId);
/// <summary>A deterministic offset page of wallet history for unchanged data.</summary>
/// <param name="Items">Accepted entries ordered by createdAt descending, then internal transaction ID descending.</param>
/// <param name="Page">The requested one-based page.</param>
/// <param name="PageSize">The requested bounded page size.</param>
/// <param name="HasMore">Whether at least one more entry existed when this page was read.</param>
/// <remarks>New commits between requests can shift offset-page boundaries.</remarks>
public sealed record HistoryPage(IReadOnlyList<HistoryItem> Items, int Page, int PageSize, bool HasMore);
public interface IWalletReader
{
    Task<PlayerBalances?> BalancesAsync(Guid playerId, CancellationToken ct);
    Task<HistoryPage?> HistoryAsync(HistoryRequest request, CancellationToken ct);
}
public sealed class ReadWallets(IWalletReader reader, IValidator<HistoryRequest> validator)
{
    public async Task<PlayerBalances> BalancesAsync(Guid playerId, CancellationToken ct)
    {
        if (playerId == Guid.Empty) throw new WalletFailure(400, "invalid_request", "Invalid player ID.");
        return await reader.BalancesAsync(playerId, ct) ?? throw new WalletFailure(404, "wallet_unavailable", "Wallets are not available yet.");
    }
    public async Task<HistoryPage> HistoryAsync(HistoryRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) throw new WalletFailure(400, "invalid_request", "Invalid history query.");
        return await reader.HistoryAsync(request, ct) ?? throw new WalletFailure(404, "wallet_unavailable", "Wallet is not available yet.");
    }
}


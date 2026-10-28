using FluentValidation;

namespace MiniWallet.Application;

public sealed record WalletBalance(string Currency, string Balance);
public sealed record PlayerBalances(Guid PlayerId, IReadOnlyList<WalletBalance> Wallets);
public sealed record HistoryRequest(Guid PlayerId, string Currency, int Page = 1, int PageSize = 20);
public sealed record HistoryItem(Guid TransactionId, string ExternalTransactionId, string Type,
    string Amount, string BalanceAfter, DateTimeOffset CreatedAt, Guid? OriginalBetId);
public sealed record HistoryPage(IReadOnlyList<HistoryItem> Items, int Page, int PageSize, bool HasMore);
public interface IWalletReader
{
    Task<PlayerBalances?> BalancesAsync(Guid playerId, CancellationToken ct);
    Task<HistoryPage?> HistoryAsync(HistoryRequest request, CancellationToken ct);
}
public sealed class HistoryValidator : AbstractValidator<HistoryRequest>
{
    public HistoryValidator()
    {
        RuleFor(x => x.PlayerId).NotEmpty();
        RuleFor(x => x.Currency).Must(x => x is "USD" or "EUR" or "BTC");
        RuleFor(x => x.Page).InclusiveBetween(1, 10_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
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


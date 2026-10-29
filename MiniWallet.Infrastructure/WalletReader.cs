using Microsoft.EntityFrameworkCore;
using MiniWallet.Application;
using MiniWallet.Domain.Entities;

namespace MiniWallet.Infrastructure;

public sealed class WalletReader(WalletDbContext db) : IWalletReader
{
    public async Task<PlayerBalances?> BalancesAsync(Guid playerId, CancellationToken ct)
    {
        var wallets = await db.Wallets.AsNoTracking().Where(x => x.PlayerId == playerId).ToListAsync(ct);
        return wallets.Count == 0 ? null : new(playerId, wallets.OrderBy(x => x.Currency.Code)
            .Select(x => new WalletBalance(x.Currency.Code, Money.Balance(x.Currency, x.Balance).Format())).ToArray());
    }
    public async Task<HistoryPage?> HistoryAsync(HistoryRequest request, CancellationToken ct)
    {
        var currency = Currency.Parse(request.Currency);
        if (!await db.Wallets.AnyAsync(x => x.PlayerId == request.PlayerId && x.Currency == currency, ct)) return null;
        var entries = await db.Entries.AsNoTracking().Where(x => x.PlayerId == request.PlayerId && x.Currency == currency)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize + 1).ToListAsync(ct);
        return new(entries.Take(request.PageSize).Select(x => new HistoryItem(x.Id, x.ExternalTransactionId, x.Type,
            Money.Balance(x.Currency, x.Amount).Format(), Money.Balance(x.Currency, x.BalanceAfter).Format(), x.CreatedAt, x.OriginalBetId)).ToArray(),
            request.Page, request.PageSize, entries.Count > request.PageSize);
    }
}


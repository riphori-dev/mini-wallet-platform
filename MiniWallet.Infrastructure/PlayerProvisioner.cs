using Microsoft.EntityFrameworkCore;
using MiniWallet.Application;
using MiniWallet.Domain;

namespace MiniWallet.Infrastructure;

public sealed class PlayerProvisioner(WalletDbContext db) : IPlayerProvisioner
{
    public async Task<ProvisioningOutcome> ProvisionAsync(PlayerRegistered registration, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var utc = registration.OccurredAt.ToUniversalTime();
        var occurredAt = utc.AddTicks(-(utc.Ticks % 10)); // PostgreSQL timestamp precision is microseconds.
        // The unique insert waits for an in-flight duplicate to commit or roll back.
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PlayerRegistrationReceipts" ("EventId", "PlayerId", "OccurredAt", "Version")
            VALUES ({registration.EventId}, {registration.PlayerId}, {occurredAt}, {registration.Version})
            ON CONFLICT ("EventId") DO NOTHING
            """, ct);
        if (inserted == 0)
        {
            var receipt = await db.RegistrationReceipts.AsNoTracking().SingleAsync(x => x.EventId == registration.EventId, ct);
            var matches = receipt.PlayerId == registration.PlayerId && receipt.Version == registration.Version
                && receipt.OccurredAt == occurredAt;
            await transaction.CommitAsync(ct);
            return matches ? ProvisioningOutcome.Accepted : ProvisioningOutcome.EventConflict;
        }

        foreach (var currency in Currency.Supported)
        {
            var wallet = PlayerWallet.Create(registration.PlayerId, currency);
            // Another event for this player must never reset or duplicate a wallet.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Wallets" ("Id", "PlayerId", "Currency", "Balance")
                VALUES ({wallet.Id}, {wallet.PlayerId}, {wallet.Currency.Code}, {wallet.Balance})
                ON CONFLICT ("PlayerId", "Currency") DO NOTHING
                """, ct);
        }
        await transaction.CommitAsync(ct);
        return ProvisioningOutcome.Accepted;
    }
}


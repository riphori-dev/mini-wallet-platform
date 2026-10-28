using Microsoft.EntityFrameworkCore;
using MiniWallet.Application;
using MiniWallet.Domain;

namespace MiniWallet.Infrastructure;

public static class DevelopmentWalletFixture
{
    public static readonly Guid PlayerId = Guid.Parse("de000000-0000-4000-8000-000000000001");
    public static async Task SeedAsync(WalletDbContext db)
    {
        await new PlayerProvisioner(db).ProvisionAsync(new(Guid.Parse("de000000-0000-4000-8000-000000000002"),
            PlayerId, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), default);
        foreach (var currency in Currency.Supported)
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var wallet = await db.Wallets.FromSqlInterpolated(
                $"SELECT * FROM \"Wallets\" WHERE \"PlayerId\" = {PlayerId} AND \"Currency\" = {currency.Code} FOR UPDATE").SingleAsync();
            var reference = "opening-" + currency.Code;
            if (!await db.Entries.AnyAsync(x => x.ProviderId == "development-fixture" && x.ExternalTransactionId == reference))
            {
                var amount = Money.Amount(currency, currency == Currency.Btc ? 0.01m : 100m);
                wallet.OpeningCredit(amount);
                db.Entries.Add(FinancialEntry.Record(wallet, "development-fixture", reference, "DEV_FUNDING", amount, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }
            await transaction.CommitAsync();
        }
    }
}


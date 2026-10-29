using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniWallet.Application;
using MiniWallet.Application.Interfaces;
using MiniWallet.Domain.Entities;

namespace MiniWallet.Infrastructure;

public sealed class TransactionStore(WalletDbContext db, TimeProvider clock) : ITransactionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<StoredResponse> ExecuteAsync(TransactionCommand command, ITransactionHandler handler, string traceId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'; SET LOCAL statement_timeout = '5s';", ct);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TransactionOutcomes"
              ("ProviderId","ExternalTransactionId","PlayerId","Currency","Type","Amount","BetExternalTransactionId","Status","Response")
            VALUES ({command.ProviderId},{command.ExternalTransactionId},{command.PlayerId},{command.Currency.Code},
              {command.Type},{command.Amount.Value},{command.BetExternalTransactionId},0,NULL)
            ON CONFLICT ("ProviderId","ExternalTransactionId") DO NOTHING
            """, ct);
        var outcome = await db.TransactionOutcomes.SingleAsync(x => x.ProviderId == command.ProviderId
            && x.ExternalTransactionId == command.ExternalTransactionId, ct);
        if (inserted == 0)
        {
            if (!outcome.Matches(command)) throw new WalletFailure(409, "idempotency_conflict", "Transaction ID already has a different payload.");
            await transaction.CommitAsync(ct);
            return new(outcome.Status, outcome.Response!, outcome.Status == 201 ? "application/json" : "application/problem+json");
        }

        var wallet = await db.Wallets.FromSqlInterpolated($"""
            SELECT * FROM "Wallets" WHERE "PlayerId" = {command.PlayerId} AND "Currency" = {command.Currency.Code} FOR UPDATE
            """).SingleOrDefaultAsync(ct);
        if (wallet is null) throw new WalletFailure(404, "wallet_unavailable", "Wallet is not provisioned; retry this ID after provisioning.");
        FinancialEntry? bet = null;
        var settled = false;
        if (command.Type == "WIN")
        {
            bet = await db.Entries.AsNoTracking().SingleOrDefaultAsync(x => x.ProviderId == command.ProviderId
                && x.ExternalTransactionId == command.BetExternalTransactionId, ct);
            if (bet is not null) settled = await db.Entries.AnyAsync(x => x.OriginalBetId == bet.Id, ct);
        }
        try
        {
            var now = clock.GetUtcNow();
            now = now.AddTicks(-(now.Ticks % 10));
            var entry = handler.Apply(wallet, command, bet, settled, now);
            db.Entries.Add(entry);
            outcome.Status = 201;
            outcome.Response = JsonSerializer.Serialize(new TransactionResponse(entry.Id, entry.ExternalTransactionId,
                entry.PlayerId, entry.Currency.Code, entry.Type, Money.Amount(entry.Currency, entry.Amount).Format(),
                Money.Balance(entry.Currency, entry.BalanceAfter).Format(), entry.CreatedAt, command.BetExternalTransactionId), Json);
        }
        catch (FinancialRuleException ex)
        {
            outcome.Status = 409;
            outcome.Response = JsonSerializer.Serialize(new TransactionProblem("urn:miniwallet:problem:" + ex.Code,
                "Transaction rejected", 409, ex.Code.Replace('_', ' '), ex.Code, traceId), Json);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(outcome.Status, outcome.Response!, outcome.Status == 201 ? "application/json" : "application/problem+json");
    }
}

public sealed class TransactionOutcome
{
    public string ProviderId { get; set; } = "";
    public string ExternalTransactionId { get; set; } = "";
    public Guid PlayerId { get; set; }
    public string Currency { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public string? BetExternalTransactionId { get; set; }
    public int Status { get; set; }
    public string? Response { get; set; }
    public bool Matches(TransactionCommand command) => PlayerId == command.PlayerId && Currency == command.Currency.Code
        && Type == command.Type && Amount == command.Amount.Value && BetExternalTransactionId == command.BetExternalTransactionId;
}


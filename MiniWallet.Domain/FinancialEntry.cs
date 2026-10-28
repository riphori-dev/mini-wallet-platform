namespace MiniWallet.Domain;

public sealed class FinancialEntry
{
    private FinancialEntry() { }
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public Guid PlayerId { get; private set; }
    public Currency Currency { get; private set; } = null!;
    public string ProviderId { get; private set; } = "";
    public string ExternalTransactionId { get; private set; } = "";
    public string Type { get; private set; } = "";
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public Guid? OriginalBetId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static FinancialEntry Record(PlayerWallet wallet, string provider, string externalId,
        string type, Money amount, DateTimeOffset createdAt, Guid? betId = null)
    {
        if (amount.Value <= 0 || type is not ("BET" or "WIN" or "DEV_FUNDING") || amount.Currency != wallet.Currency
            || (type == "WIN") != betId.HasValue) throw new FinancialRuleException("invalid_entry");
        return new()
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            PlayerId = wallet.PlayerId,
            Currency = wallet.Currency,
            ProviderId = provider,
            ExternalTransactionId = externalId,
            Type = type,
            Amount = amount.Value,
            BalanceAfter = wallet.Balance,
            OriginalBetId = betId,
            CreatedAt = createdAt
        };
    }
}


namespace MiniWallet.Domain.Entities;

/// <summary>A ledger snapshot of an accepted wallet operation.</summary>
/// <remarks>Record captures the wallet after mutation; it does not debit, credit or persist it. BET lookup and settlement uniqueness belong to the use case and persistence boundary.</remarks>
public sealed class FinancialEntry
{
    private FinancialEntry() { }
    /// <summary>The internal transaction identifier, distinct from the provider reference.</summary>
    public Guid Id { get; private set; }
    /// <summary>The internal wallet identifier captured from the wallet.</summary>
    public Guid WalletId { get; private set; }
    /// <summary>The wallet owner captured from the wallet.</summary>
    public Guid PlayerId { get; private set; }
    /// <summary>The denomination of the wallet and amount.</summary>
    public Currency Currency { get; private set; } = null!;
    /// <summary>The provider identity supplied by the use case; development funding uses a fixture identity.</summary>
    public string ProviderId { get; private set; } = "";
    /// <summary>The external reference supplied by the provider or development fixture.</summary>
    public string ExternalTransactionId { get; private set; } = "";
    /// <summary>The operation kind: BET, WIN or DEV_FUNDING.</summary>
    public string Type { get; private set; } = "";
    /// <summary>The strictly positive operation amount, not a signed balance delta.</summary>
    public decimal Amount { get; private set; }
    /// <summary>The wallet balance at the time the entry is recorded.</summary>
    public decimal BalanceAfter { get; private set; }
    /// <summary>The internal BET entry identifier for WIN; null for other kinds.</summary>
    public Guid? OriginalBetId { get; private set; }
    /// <summary>The operation timestamp supplied by the caller.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Captures an operation and the wallet balance after its application.</summary>
    /// <param name="wallet">The affected wallet, already mutated by the business operation.</param>
    /// <param name="provider">The provider or fixture identity; this method does not authenticate it.</param>
    /// <param name="externalId">The external transaction reference.</param>
    /// <param name="type">BET, WIN or DEV_FUNDING.</param>
    /// <param name="amount">A positive amount in the wallet currency.</param>
    /// <param name="createdAt">The timestamp to capture.</param>
    /// <param name="betId">Required exactly for WIN; the internal BET identifier.</param>
    /// <returns>A new ledger entry with a generated internal identifier.</returns>
    /// <exception cref="FinancialRuleException">The amount is nonpositive, currency differs, type is unsupported, or BET-reference presence disagrees with the type.</exception>
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


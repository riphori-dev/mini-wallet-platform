namespace MiniWallet.Domain;

public sealed class PlayerWallet
{
    private PlayerWallet() { }
    public Guid Id { get; private set; }
    public Guid PlayerId { get; private set; }
    public Currency Currency { get; private set; } = null!;
    public decimal Balance { get; private set; }

    public void Bet(Money amount) => Balance = Money.Balance(Currency, Balance).Debit(amount).Value;
    public void OpeningCredit(Money amount) => Balance = Money.Balance(Currency, Balance).Credit(amount).Value;
    public void Win(Money amount, FinancialEntry? bet, string providerId, bool alreadySettled)
    {
        if (bet is null || bet.Type != "BET") throw new FinancialRuleException("bet_not_found");
        if (bet.ProviderId != providerId || bet.PlayerId != PlayerId || bet.Currency != Currency)
            throw new FinancialRuleException("bet_mismatch");
        if (alreadySettled) throw new FinancialRuleException("bet_already_settled");
        Balance = Money.Balance(Currency, Balance).Credit(amount).Value;
    }

    public static PlayerWallet Create(Guid playerId, Currency currency)
    {
        if (playerId == Guid.Empty) throw new ArgumentException("Player identity is required.");
        ArgumentNullException.ThrowIfNull(currency);
        return new() { Id = Guid.NewGuid(), PlayerId = playerId, Currency = currency, Balance = 0m };
    }
}


namespace MiniWallet.Domain.Entities;

/// <summary>A balance owned by one player in one supported currency.</summary>
/// <remarks>Creation starts at zero. Database uniqueness, ownership authorization and concurrent write protection are enforced outside this entity.</remarks>
public sealed class PlayerWallet
{
    private PlayerWallet() { }
    /// <summary>The internal wallet identifier.</summary>
    public Guid Id { get; private set; }
    /// <summary>The owning player identifier assigned at creation.</summary>
    public Guid PlayerId { get; private set; }
    /// <summary>The wallet denomination, fixed at creation.</summary>
    public Currency Currency { get; private set; } = null!;
    /// <summary>The nonnegative balance maintained within Money bounds and currency precision.</summary>
    public decimal Balance { get; private set; }

    /// <summary>Debits a positive stake from this wallet.</summary>
    /// <param name="amount">A same-currency positive stake.</param>
    /// <exception cref="FinancialRuleException">The currency differs, amount is nonpositive or funds are insufficient.</exception>
    public void Bet(Money amount) => Balance = Money.Balance(Currency, Balance).Debit(amount).Value;
    /// <summary>Applies a positive opening credit used by the development fixture.</summary>
    /// <param name="amount">A positive same-currency credit.</param>
    /// <exception cref="FinancialRuleException">The currency differs, amount is nonpositive or the balance ceiling would be exceeded.</exception>
    /// <remarks>This method itself does not restrict the environment; the fixture caller does.</remarks>
    public void OpeningCredit(Money amount) => Balance = Money.Balance(Currency, Balance).Credit(amount).Value;
    /// <summary>Credits a payout against a matching earlier BET.</summary>
    /// <param name="amount">The positive same-currency payout; it need not equal the stake.</param>
    /// <param name="bet">The earlier entry, which must be a BET for this provider, player and currency.</param>
    /// <param name="providerId">The provider identity supplied by the authenticated use case.</param>
    /// <param name="alreadySettled">Whether persistence found a previous settlement for the BET.</param>
    /// <exception cref="FinancialRuleException">The BET is absent, mismatched or settled, or the credit violates money rules.</exception>
    /// <remarks>The caller loads the accepted BET and settlement state under database coordination. One WIN per BET is an implementation assumption, also constrained in PostgreSQL.</remarks>
    public void Win(Money amount, FinancialEntry? bet, string providerId, bool alreadySettled)
    {
        if (bet is null || bet.Type != "BET") throw new FinancialRuleException("bet_not_found");
        if (bet.ProviderId != providerId || bet.PlayerId != PlayerId || bet.Currency != Currency)
            throw new FinancialRuleException("bet_mismatch");
        if (alreadySettled) throw new FinancialRuleException("bet_already_settled");
        Balance = Money.Balance(Currency, Balance).Credit(amount).Value;
    }

    /// <summary>Creates a zero-balance wallet for a nonempty player identity.</summary>
    /// <param name="playerId">The owner; cannot be the empty GUID.</param>
    /// <param name="currency">The supported denomination.</param>
    /// <returns>A new wallet with a generated internal identifier.</returns>
    /// <exception cref="ArgumentException">The player identity is empty.</exception>
    /// <exception cref="ArgumentNullException">The currency is null.</exception>
    public static PlayerWallet Create(Guid playerId, Currency currency)
    {
        if (playerId == Guid.Empty) throw new ArgumentException("Player identity is required.");
        ArgumentNullException.ThrowIfNull(currency);
        return new() { Id = Guid.NewGuid(), PlayerId = playerId, Currency = currency, Balance = 0m };
    }
}


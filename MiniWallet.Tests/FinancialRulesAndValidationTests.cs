using FluentValidation;
using MiniWallet.Application;
using MiniWallet.Domain;
using Xunit;

namespace MiniWallet.Tests;

public sealed class FinancialRulesAndValidationTests
{
    [Theory]
    [InlineData("missing", "bet_not_found")]
    [InlineData("provider", "bet_mismatch")]
    [InlineData("player", "bet_mismatch")]
    [InlineData("currency", "bet_mismatch")]
    [InlineData("settled", "bet_already_settled")]
    public void Win_handler_rejections_do_not_change_balance(string scenario, string code)
    {
        var wallet = PlayerWallet.Create(Guid.NewGuid(), Currency.Usd);
        wallet.OpeningCredit(Money.Amount(Currency.Usd, 100m));
        var betCommand = new TransactionCommand("provider", "bet", wallet.PlayerId,
            Currency.Usd, "BET", Money.Amount(Currency.Usd, 10m), null);
        var bet = new BetHandler().Apply(wallet, betCommand, null, false, DateTimeOffset.UtcNow);
        var target = scenario switch
        {
            "player" => PlayerWallet.Create(Guid.NewGuid(), Currency.Usd),
            "currency" => PlayerWallet.Create(wallet.PlayerId, Currency.Eur),
            _ => wallet
        };
        var before = target.Balance;
        var win = new TransactionCommand(scenario == "provider" ? "wrong" : "provider", "win",
            target.PlayerId, target.Currency, "WIN", Money.Amount(target.Currency, 25m), "bet");
        var failure = Assert.Throws<FinancialRuleException>(() =>
            new WinHandler().Apply(target, win, scenario == "missing" ? null : bet,
                scenario == "settled", DateTimeOffset.UtcNow));
        Assert.Equal(code, failure.Code);
        Assert.Equal(before, target.Balance);
    }

    [Fact]
    public void Exact_balance_bet_and_positive_win_are_domain_rules()
    {
        var wallet = PlayerWallet.Create(Guid.NewGuid(), Currency.Eur);
        wallet.OpeningCredit(Money.Amount(Currency.Eur, 100m));
        var command = new TransactionCommand("provider", "exact", wallet.PlayerId,
            Currency.Eur, "BET", Money.Amount(Currency.Eur, 100m), null);
        var bet = new BetHandler().Apply(wallet, command, null, false, DateTimeOffset.UtcNow);
        Assert.Equal(0m, wallet.Balance);
        var win = command with
        {
            Type = "WIN",
            ExternalTransactionId = "win",
            Amount = Money.Amount(Currency.Eur, 125m),
            BetExternalTransactionId = "exact"
        };
        var entry = new WinHandler().Apply(wallet, win, bet, false, DateTimeOffset.UtcNow);
        Assert.Equal(125m, wallet.Balance);
        Assert.Equal(bet.Id, entry.OriginalBetId);
        Assert.Equal(125m, entry.BalanceAfter);
    }

    [Theory]
    [InlineData("unsupported-currency")]
    [InlineData("lowercase-currency")]
    [InlineData("unsupported-type")]
    [InlineData("win-without-reference")]
    [InlineData("bet-with-reference")]
    [InlineData("empty-player")]
    public async Task Transaction_validation_rejects_invalid_contract_inputs(string scenario)
    {
        var valid = new TransactionRequest("id", Guid.NewGuid(), "USD", "BET", "1.00");
        var request = scenario switch
        {
            "unsupported-currency" => valid with { Currency = "JPY" },
            "lowercase-currency" => valid with { Currency = "usd" },
            "unsupported-type" => valid with { Type = "REFUND" },
            "win-without-reference" => valid with { Type = "WIN" },
            "bet-with-reference" => valid with { BetExternalTransactionId = "other" },
            "empty-player" => valid with { PlayerId = Guid.Empty },
            _ => throw new ArgumentException(nameof(scenario))
        };
        Assert.False((await new TransactionValidator().ValidateAsync(request)).IsValid);
    }

    [Fact]
    public async Task Asynchronous_validation_rejects_before_reserving_persistence()
    {
        var validator = new InlineValidator<TransactionRequest>();
        validator.RuleFor(x => x).CustomAsync(async (_, context, ct) =>
        {
            await Task.Yield();
            context.AddFailure("ExternalTransactionId", "Rejected asynchronously.");
        });
        var store = new RecordingStore();
        var useCase = new SubmitTransaction(validator, store, [new BetHandler(), new WinHandler()]);
        var failure = await Assert.ThrowsAsync<WalletFailure>(() => useCase.ExecuteAsync("provider",
            new("id", Guid.NewGuid(), "USD", "BET", "1.00"), "trace", default));
        Assert.Equal(400, failure.Status);
        Assert.False(store.Invoked);
    }

    // Only a unit-test sentinel for validation ordering; not evidence of database correctness.
    private sealed class RecordingStore : ITransactionStore
    {
        public bool Invoked { get; private set; }
        public Task<StoredResponse> ExecuteAsync(TransactionCommand command, ITransactionHandler handler, string traceId, CancellationToken ct)
        {
            Invoked = true;
            throw new InvalidOperationException("Invalid input must not reach persistence.");
        }
    }
}


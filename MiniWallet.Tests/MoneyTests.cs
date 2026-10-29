using MiniWallet.Application;
using MiniWallet.Application.Validations;
using MiniWallet.Domain.Entities;
using Xunit;

namespace MiniWallet.Tests;

public sealed class MoneyTests
{
    [Fact]
    public async Task External_identifiers_cannot_contain_a_trailing_newline()
    {
        var validation = await new TransactionValidator().ValidateAsync(
            new TransactionRequest("bet\n", Guid.NewGuid(), "USD", "BET", "1.00"));
        Assert.False(validation.IsValid);
    }
    [Theory]
    [InlineData("USD", "0.01", true)]
    [InlineData("EUR", "1.230", false)]
    [InlineData("BTC", "0.00000001", true)]
    [InlineData("BTC", "0.000000001", false)]
    [InlineData("USD", "-1", false)]
    [InlineData("USD", "1e2", false)]
    [InlineData("USD", "0", false)]
    [InlineData("USD", "1,000.00", false)]
    [InlineData("BTC", "99999999999999999999.99999999", true)]
    [InlineData("USD", "99999999999999999999.99", true)]
    [InlineData("USD", "100000000000000000000.00", false)]
    public void Amount_text_has_explicit_precision_and_bounds(string currency, string text, bool valid) =>
        Assert.Equal(valid, AmountText.TryParse(currency, text, out _));

    [Fact]
    public void Domain_prevents_overdraft_cross_currency_zero_and_overflow()
    {
        var balance = Money.Balance(Currency.Usd, 10m);
        Assert.Equal("9.99", balance.Debit(Money.Amount(Currency.Usd, 0.01m)).Format());
        Assert.Throws<FinancialRuleException>(() => balance.Debit(Money.Amount(Currency.Usd, 11m)));
        Assert.Throws<FinancialRuleException>(() => balance.Credit(Money.Amount(Currency.Eur, 1m)));
        Assert.Throws<FinancialRuleException>(() => balance.Debit(Money.Balance(Currency.Usd, 0m)));
        Assert.Throws<FinancialRuleException>(() => Money.Balance(Currency.Btc, Money.Maximum).Credit(Money.Amount(Currency.Btc, 0.00000001m)));
    }
}


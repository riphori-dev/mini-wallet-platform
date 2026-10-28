using System.Globalization;

namespace MiniWallet.Domain;

public sealed class FinancialRuleException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
public sealed record Money
{
    public const decimal Maximum = 99999999999999999999.99999999m;
    public Currency Currency { get; }
    public decimal Value { get; }
    private Money(Currency currency, decimal value) => (Currency, Value) = (currency, value);

    public static Money Amount(Currency currency, decimal value)
    {
        if (value <= 0 || value > Maximum) throw new FinancialRuleException("invalid_amount");
        var scale = (decimal.GetBits(value)[3] >> 16) & 0xff;
        if (scale > currency.Scale) throw new FinancialRuleException("invalid_precision");
        return new(currency, value);
    }
    public static Money Balance(Currency currency, decimal value)
    {
        if (value < 0 || value > Maximum || decimal.Round(value, currency.Scale) != value)
            throw new FinancialRuleException("invalid_balance");
        // Strip storage trailing zeroes only; never change the monetary value.
        return new(currency, decimal.Round(value, currency.Scale));
    }
    public Money Debit(Money amount)
    {
        SameCurrency(amount);
        if (amount.Value <= 0) throw new FinancialRuleException("invalid_amount");
        if (amount.Value > Value) throw new FinancialRuleException("insufficient_funds");
        return Balance(Currency, checked(Value - amount.Value));
    }
    public Money Credit(Money amount)
    {
        SameCurrency(amount);
        if (amount.Value <= 0) throw new FinancialRuleException("invalid_amount");
        decimal result;
        try { result = checked(Value + amount.Value); }
        catch (OverflowException) { throw new FinancialRuleException("balance_limit_exceeded"); }
        if (result > Maximum) throw new FinancialRuleException("balance_limit_exceeded");
        return Balance(Currency, result);
    }
    private void SameCurrency(Money other)
    {
        if (Currency != other.Currency) throw new FinancialRuleException("currency_mismatch");
    }
    public string Format() => Value.ToString("F" + Currency.Scale, CultureInfo.InvariantCulture);
}


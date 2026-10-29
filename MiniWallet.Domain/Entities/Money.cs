using System.Globalization;

namespace MiniWallet.Domain.Entities;

/// <summary>A rejected financial operation identified by a stable business error code.</summary>
/// <param name="code">The business failure code, also used as the exception message.</param>
public sealed class FinancialRuleException(string code) : Exception(code)
{
    /// <summary>The stable financial rejection code.</summary>
    public string Code { get; } = code;
}
/// <summary>A decimal monetary value bound to one supported currency.</summary>
/// <remarks>Factories distinguish positive transaction amounts from nonnegative balances. Arithmetic rejects cross-currency operands.</remarks>
public sealed record Money
{
    /// <summary>The inclusive amount and balance ceiling, matching PostgreSQL numeric(28,8).</summary>
    /// <remarks>USD/EUR precision further limits their effective maximum to 99999999999999999999.99.</remarks>
    public const decimal Maximum = 99999999999999999999.99999999m;
    /// <summary>The denomination governing precision and arithmetic compatibility.</summary>
    public Currency Currency { get; }
    /// <summary>The validated decimal amount or balance.</summary>
    public decimal Value { get; }
    private Money(Currency currency, decimal value) => (Currency, Value) = (currency, value);

    /// <summary>Creates a strictly positive transaction amount without rounding.</summary>
    /// <param name="currency">The supported denomination.</param>
    /// <param name="value">A value no greater than Maximum, with decimal scale no greater than the currency scale.</param>
    /// <returns>A positive monetary value.</returns>
    /// <exception cref="FinancialRuleException">The amount is nonpositive, exceeds Maximum or has excess decimal scale, including trailing zeroes.</exception>
    public static Money Amount(Currency currency, decimal value)
    {
        if (value <= 0 || value > Maximum) throw new FinancialRuleException("invalid_amount");
        var scale = (decimal.GetBits(value)[3] >> 16) & 0xff;
        if (scale > currency.Scale) throw new FinancialRuleException("invalid_precision");
        return new(currency, value);
    }
    /// <summary>Creates a nonnegative balance without changing its monetary value.</summary>
    /// <param name="currency">The supported denomination.</param>
    /// <param name="value">A nonnegative value within Maximum, exactly representable at the currency scale.</param>
    /// <returns>A balance normalized to currency scale.</returns>
    /// <exception cref="FinancialRuleException">The value is negative, exceeds Maximum or requires rounding.</exception>
    public static Money Balance(Currency currency, decimal value)
    {
        if (value < 0 || value > Maximum || decimal.Round(value, currency.Scale) != value)
            throw new FinancialRuleException("invalid_balance");
        // Strip storage trailing zeroes only; never change the monetary value.
        return new(currency, decimal.Round(value, currency.Scale));
    }
    /// <summary>Subtracts a positive same-currency amount without allowing an overdraft.</summary>
    /// <param name="amount">The stake to subtract.</param>
    /// <returns>A new nonnegative balance.</returns>
    /// <exception cref="FinancialRuleException">Currencies differ, the amount is nonpositive or funds are insufficient.</exception>
    public Money Debit(Money amount)
    {
        SameCurrency(amount);
        if (amount.Value <= 0) throw new FinancialRuleException("invalid_amount");
        if (amount.Value > Value) throw new FinancialRuleException("insufficient_funds");
        return Balance(Currency, checked(Value - amount.Value));
    }
    /// <summary>Adds a positive same-currency amount within the balance ceiling.</summary>
    /// <param name="amount">The payout or opening credit to add.</param>
    /// <returns>A new balance.</returns>
    /// <exception cref="FinancialRuleException">Currencies differ, the amount is nonpositive or the resulting balance exceeds Maximum, including decimal overflow.</exception>
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
    /// <summary>Formats the value as an invariant decimal string with exactly the currency scale.</summary>
    /// <returns>A fixed-scale string without grouping or an exponent.</returns>
    public string Format() => Value.ToString("F" + Currency.Scale, CultureInfo.InvariantCulture);
}


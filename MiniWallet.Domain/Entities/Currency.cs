namespace MiniWallet.Domain.Entities;

/// <summary>A supported wallet denomination with its permitted fractional precision.</summary>
public sealed record Currency
{
    /// <summary>The case-sensitive ISO-style code: USD, EUR or BTC.</summary>
    public string Code { get; }
    /// <summary>The maximum fractional digits for amounts: two for USD/EUR and eight for BTC.</summary>
    public int Scale { get; }
    private Currency(string code, int scale) => (Code, Scale) = (code, scale);
    /// <summary>US dollars with two fractional digits.</summary>
    public static Currency Usd { get; } = new("USD", 2);
    /// <summary>Euros with two fractional digits.</summary>
    public static Currency Eur { get; } = new("EUR", 2);
    /// <summary>Bitcoin with eight fractional digits.</summary>
    public static Currency Btc { get; } = new("BTC", 8);
    /// <summary>The read-only list of USD, EUR and BTC supported for provisioning.</summary>
    public static IReadOnlyList<Currency> Supported { get; } = Array.AsReadOnly(new[] { Usd, Eur, Btc });
    /// <summary>Resolves an exact supported currency code.</summary>
    /// <param name="code">Case-sensitive USD, EUR or BTC.</param>
    /// <returns>The corresponding currency value.</returns>
    /// <exception cref="ArgumentException">The code is unsupported.</exception>
    public static Currency Parse(string code) => code switch
    {
        "USD" => Usd,
        "EUR" => Eur,
        "BTC" => Btc,
        _ => throw new ArgumentException("Unsupported currency.", nameof(code))
    };
}


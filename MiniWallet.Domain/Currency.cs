namespace MiniWallet.Domain;

public sealed record Currency
{
    public string Code { get; }
    public int Scale { get; }
    private Currency(string code, int scale) => (Code, Scale) = (code, scale);
    public static Currency Usd { get; } = new("USD", 2);
    public static Currency Eur { get; } = new("EUR", 2);
    public static Currency Btc { get; } = new("BTC", 8);
    public static IReadOnlyList<Currency> Supported { get; } = Array.AsReadOnly(new[] { Usd, Eur, Btc });
    public static Currency Parse(string code) => code switch
    {
        "USD" => Usd,
        "EUR" => Eur,
        "BTC" => Btc,
        _ => throw new ArgumentException("Unsupported currency.", nameof(code))
    };
}


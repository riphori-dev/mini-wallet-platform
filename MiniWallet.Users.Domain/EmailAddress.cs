namespace MiniWallet.Users.Domain;

/// <summary>The platform treats the whole email address as case-insensitive.</summary>
public sealed record EmailAddress
{
    public string Value { get; }
    private EmailAddress(string value) => Value = value;
    public static EmailAddress Parse(string input)
    {
        var value = input.Trim().ToLowerInvariant();
        if (value.Length is < 3 or > 254 || value.Any(c => char.IsWhiteSpace(c) || c > 127)
            || !System.Net.Mail.MailAddress.TryCreate(value, out var parsed)
            || parsed.Address != value)
            throw new ArgumentException("Invalid email address.");
        return new EmailAddress(value);
    }
}


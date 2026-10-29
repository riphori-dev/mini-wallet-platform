namespace MiniWallet.Users.Domain.Entities;

/// <summary>The platform treats the whole email address as case-insensitive.</summary>
public sealed record EmailAddress
{
    /// <summary>The trimmed, invariant-lowercase ASCII email address.</summary>
    public string Value { get; }
    private EmailAddress(string value) => Value = value;
    /// <summary>Normalizes and validates an email address.</summary>
    /// <param name="input">An address to trim and lowercase.</param>
    /// <returns>A normalized address of 3 to 254 characters without whitespace or non-ASCII characters.</returns>
    /// <exception cref="ArgumentException">The normalized input fails length, character or MailAddress parsing checks.</exception>
    /// <remarks>The entire address is treated as case-insensitive; display-name syntax is rejected.</remarks>
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


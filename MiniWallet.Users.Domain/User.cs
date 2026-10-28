namespace MiniWallet.Users.Domain;

public sealed class User
{
    private User() { }
    public Guid Id { get; private set; }
    public string Email { get; private set; } = "";
    public string PasswordHash { get; private set; } = "";
    public string Role { get; private set; } = "player";
    public bool EmailVerified { get; private set; }
    public string? VerificationTokenHash { get; private set; }
    public DateTimeOffset? VerificationExpiresAt { get; private set; }

    public static User Register(EmailAddress email, string passwordHash) =>
        new() { Id = Guid.NewGuid(), Email = email.Value, PasswordHash = passwordHash };

    public static User DevelopmentPlayer(EmailAddress email, string passwordHash) =>
        new()
        {
            Id = Guid.Parse("de000000-0000-4000-8000-000000000001"),
            Email = email.Value,
            PasswordHash = passwordHash,
            EmailVerified = true
        };

    // Called only by explicitly configured Development seeding, never registration.
    public static User DevelopmentAdmin(EmailAddress email, string passwordHash) =>
        new()
        {
            Id = Guid.Parse("ad000000-0000-4000-8000-000000000001"),
            Email = email.Value,
            PasswordHash = passwordHash,
            Role = "admin",
            EmailVerified = true
        };

    public void SetVerification(string hash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (EmailVerified || expiresAt <= now || string.IsNullOrWhiteSpace(hash))
            throw new InvalidOperationException("Cannot issue verification.");
        VerificationTokenHash = hash;
        VerificationExpiresAt = expiresAt;
    }

    public bool Verify(string hash, DateTimeOffset now)
    {
        if (EmailVerified || VerificationTokenHash != hash || VerificationExpiresAt <= now
            || VerificationExpiresAt is null) return false;
        EmailVerified = true;
        VerificationTokenHash = null;
        VerificationExpiresAt = null;
        return true;
    }
}


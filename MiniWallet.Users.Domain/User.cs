namespace MiniWallet.Users.Domain;

/// <summary>A registered user with credentials, role and email-verification state.</summary>
/// <remarks>Public registration assigns the player role. Token hashing, password hashing, email uniqueness and atomic persistence are provided outside this entity.</remarks>
public sealed class User
{
    private User() { }
    /// <summary>The Users identity used as the player subject and wallet owner.</summary>
    public Guid Id { get; private set; }
    /// <summary>The normalized email copied from EmailAddress.</summary>
    public string Email { get; private set; } = "";
    /// <summary>The password hash supplied by the credential implementation; never the plaintext password.</summary>
    public string PasswordHash { get; private set; } = "";
    /// <summary>The player role, or admin for the explicit development admin factory.</summary>
    public string Role { get; private set; } = "player";
    /// <summary>Whether an email token was consumed or a verified development identity was created.</summary>
    public bool EmailVerified { get; private set; }
    /// <summary>The pending token hash, cleared on successful verification.</summary>
    public string? VerificationTokenHash { get; private set; }
    /// <summary>The pending token expiry, cleared on successful verification.</summary>
    public DateTimeOffset? VerificationExpiresAt { get; private set; }

    /// <summary>Creates an unverified player with a new identity.</summary>
    /// <param name="email">The normalized address.</param>
    /// <param name="passwordHash">The precomputed password hash.</param>
    /// <returns>A player without a pending verification token until SetVerification is called.</returns>
    public static User Register(EmailAddress email, string passwordHash) =>
        new() { Id = Guid.NewGuid(), Email = email.Value, PasswordHash = passwordHash };

    /// <summary>Creates the verified development demo player with its fixed identity.</summary>
    /// <param name="email">The demo address.</param>
    /// <param name="passwordHash">The precomputed password hash.</param>
    /// <returns>The verified demo player.</returns>
    /// <remarks>The factory does not check the environment; development seeding controls its use.</remarks>
    public static User DevelopmentPlayer(EmailAddress email, string passwordHash) =>
        new()
        {
            Id = Guid.Parse("de000000-0000-4000-8000-000000000001"),
            Email = email.Value,
            PasswordHash = passwordHash,
            EmailVerified = true
        };

    // Called only by explicitly configured Development seeding, never registration.
    /// <summary>Creates the verified development admin with its fixed identity.</summary>
    /// <param name="email">The admin address.</param>
    /// <param name="passwordHash">The precomputed password hash.</param>
    /// <returns>A verified admin.</returns>
    /// <remarks>The factory does not check the environment and is not used by registration.</remarks>
    public static User DevelopmentAdmin(EmailAddress email, string passwordHash) =>
        new()
        {
            Id = Guid.Parse("ad000000-0000-4000-8000-000000000001"),
            Email = email.Value,
            PasswordHash = passwordHash,
            Role = "admin",
            EmailVerified = true
        };

    /// <summary>Replaces the pending verification hash and expiry for an unverified user.</summary>
    /// <param name="hash">A nonblank token hash supplied by the hashing implementation.</param>
    /// <param name="expiresAt">An expiry strictly after now.</param>
    /// <param name="now">The comparison time.</param>
    /// <exception cref="InvalidOperationException">The user is verified, the hash is blank or expiry is not in the future.</exception>
    public void SetVerification(string hash, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (EmailVerified || expiresAt <= now || string.IsNullOrWhiteSpace(hash))
            throw new InvalidOperationException("Cannot issue verification.");
        VerificationTokenHash = hash;
        VerificationExpiresAt = expiresAt;
    }

    /// <summary>Consumes a matching, unexpired pending token hash and verifies the user.</summary>
    /// <param name="hash">The candidate token hash.</param>
    /// <param name="now">The comparison time; expiry at this instant is rejected.</param>
    /// <returns>True on first successful consumption; false for a mismatch, missing/expired token or already verified user.</returns>
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


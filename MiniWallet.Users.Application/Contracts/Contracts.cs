using MiniWallet.Users.Domain.Entities;

namespace MiniWallet.Users.Application.Contracts;

/// <summary>Anonymous player registration input; cannot select a role or identity.</summary>
/// <param name="Email">Email address normalized by trimming and invariant lowercasing; normalized length 3–254, ASCII without whitespace and valid MailAddress syntax.</param>
/// <param name="Password">Plaintext password, 12–128 characters; stored only as a password hash.</param>
public sealed record RegisterRequest(string Email, string Password);
/// <summary>User credentials for development login or verification resend.</summary>
/// <param name="Email">Email address normalized by trimming and invariant lowercasing; normalized length 3–254, ASCII without whitespace and valid MailAddress syntax.</param>
/// <param name="Password">Nonempty plaintext password, at most 128 characters; checked against the stored hash.</param>
public sealed record CredentialsRequest(string Email, string Password);
/// <summary>An email verification token submission.</summary>
/// <param name="Token">The 64-character hexadecimal token from the verification message; expires after 24 hours and is consumed once.</param>
public sealed record VerifyRequest(string Token);
/// <summary>Development machine-client credentials; these fields are authenticated before claims are issued.</summary>
/// <param name="ClientId">Configured client identifier, nonempty and at most 128 characters; not a trusted provider identity until credentials are checked.</param>
/// <param name="ClientSecret">Configured secret, nonempty and at most 256 characters.</param>
/// <param name="Scope">transactions:write for the provider client or players:provision for the separate internal client; credentials must authorize the requested scope.</param>
public sealed record MachineTokenRequest(string ClientId, string ClientSecret, string Scope);
/// <summary>Durably accepted player registration and immediate delivery status.</summary>
/// <param name="PlayerId">Generated Users identity GUID; used as wallet owner and later JWT subject.</param>
/// <param name="Email">Normalized registered email.</param>
/// <param name="EmailVerified">False for a newly registered player.</param>
/// <param name="ProvisioningStatus">pending: user and outbox committed; Wallet provisioning completes asynchronously, possibly before this response arrives.</param>
/// <param name="VerificationDeliveryStatus">sent when the email sender accepted the message, otherwise unavailable; authenticate and resend to recover.</param>
public sealed record RegistrationResult(Guid PlayerId, string Email, bool EmailVerified,
    string ProvisioningStatus, string VerificationDeliveryStatus);
/// <summary>A short-lived token from the Development-only issuer.</summary>
/// <param name="Access_token">Signed JWT for the authenticated user or configured machine, to send as Authorization: Bearer.</param>
/// <param name="Token_type">Bearer.</param>
/// <param name="Expires_in">Token lifetime in seconds; currently 600.</param>
public sealed record AccessToken(string Access_token, string Token_type, int Expires_in);
public sealed record VerificationSecret(string Token, string Hash);
public sealed record VerifiedIdentity(Guid Id, string Role);

public sealed class UseCaseFailure(int status, string code, string message,
    Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public Dictionary<string, string[]>? Errors { get; } = errors;
}

public interface IUserStore
{
    // Includes the pending PlayerRegistered delivery in the same database transaction.
    Task<bool> RegisterAsync(User user, DateTimeOffset occurredAt, CancellationToken ct);
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    Task<bool> VerifyAsync(string tokenHash, DateTimeOffset now, CancellationToken ct);
    Task<bool> ReplaceVerificationAsync(Guid userId, string hash, DateTimeOffset expiresAt,
        DateTimeOffset now, CancellationToken ct);
}
public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}
public interface IVerificationSecrets
{
    VerificationSecret Create();
    string Hash(string token);
}
public interface IEmailSender
{
    // False means delivery is unavailable; callers can authenticate and resend.
    Task<bool> SendVerificationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct);
}
public interface IAccessTokenIssuer
{
    AccessToken ForUser(VerifiedIdentity identity);
    AccessToken ForMachine(string clientId, string secret, string scope);
}


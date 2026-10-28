using MiniWallet.Users.Domain;

namespace MiniWallet.Users.Application;

public sealed record RegisterRequest(string Email, string Password);
public sealed record CredentialsRequest(string Email, string Password);
public sealed record VerifyRequest(string Token);
public sealed record MachineTokenRequest(string ClientId, string ClientSecret, string Scope);
public sealed record RegistrationResult(Guid PlayerId, string Email, bool EmailVerified,
    string ProvisioningStatus, string VerificationDeliveryStatus);
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


using System.Collections.Concurrent;
using MiniWallet.Users.Application;

namespace MiniWallet.Users.Infrastructure;

/// <summary>A sensitive verification message in the in-memory Development mailbox.</summary>
/// <param name="Email">Normalized recipient email.</param>
/// <param name="Token">Single-use 64-character hexadecimal verification token; plaintext appears only in this local message.</param>
/// <param name="ExpiresAt">UTC expiry timestamp; verification at or after this instant fails.</param>
public sealed record VerificationMessage(string Email, string Token, DateTimeOffset ExpiresAt);

public sealed class DevelopmentMailbox : IEmailSender
{
    private readonly ConcurrentQueue<VerificationMessage> messages = new();
    public Task<bool> SendVerificationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct)
    {
        messages.Enqueue(new(email, token, expiresAt));
        while (messages.Count > 100) messages.TryDequeue(out _);
        return Task.FromResult(true);
    }
    public VerificationMessage[] Read() => messages.ToArray();
}
public sealed class UnavailableEmailSender : IEmailSender
{
    public Task<bool> SendVerificationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct) =>
        Task.FromResult(false);
}


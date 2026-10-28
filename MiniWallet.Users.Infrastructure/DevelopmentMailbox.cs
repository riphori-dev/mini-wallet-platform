using System.Collections.Concurrent;
using MiniWallet.Users.Application;

namespace MiniWallet.Users.Infrastructure;

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


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MiniWallet.Users.Infrastructure;

public sealed class RegistrationDeliveryWorker(IServiceScopeFactory scopes, RegistrationDeliveryClient client,
    IOptions<RegistrationDeliveryOptions> options, TimeProvider clock, ILogger<RegistrationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Registration delivery polling failed; pending work is retained"); }
            try { await Task.Delay(TimeSpan.FromSeconds(settings.RetryDelaySeconds), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    // Public for deterministic integration scenarios; the host runs one polling loop.
    public async Task DispatchOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var now = clock.GetUtcNow();
        var pending = await db.RegistrationDeliveries.AsNoTracking()
            .Where(x => x.DeliveredAt == null && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.EventId).Take(20).ToListAsync(ct);
        foreach (var delivery in pending)
        {
            DeliveryAttempt attempt;
            try { attempt = await client.DeliverAsync(delivery, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Includes bounded HTTP timeout. No transaction or database lock spans the request.
                attempt = new(false, ex is OperationCanceledException ? "HTTP timeout" : "HTTP delivery failed");
                logger.LogWarning("Registration event {EventId} delivery failed; retained for retry", delivery.EventId);
            }
            var rows = db.RegistrationDeliveries.Where(x => x.EventId == delivery.EventId && x.DeliveredAt == null);
            if (attempt.Success)
                await rows.ExecuteUpdateAsync(set => set.SetProperty(x => x.DeliveredAt, clock.GetUtcNow())
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1).SetProperty(x => x.LastError, (string?)null), ct);
            else
            {
                var retryAt = clock.GetUtcNow().AddSeconds(options.Value.RetryDelaySeconds);
                await rows.ExecuteUpdateAsync(set => set.SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.NextAttemptAt, retryAt).SetProperty(x => x.LastError, attempt.Error), ct);
                logger.LogWarning("Registration event {EventId} not acknowledged ({Reason}); retry scheduled", delivery.EventId, attempt.Error);
            }
        }
    }
}

public static class RegistrationDeliveryServices
{
    public static IServiceCollection AddRegistrationDelivery(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RegistrationDeliveryOptions>().Bind(configuration.GetSection("Delivery"))
            .Validate(o => !o.Enabled || (o.RequestTimeoutSeconds is >= 1 and <= 30 && o.RetryDelaySeconds is >= 1 and <= 60
                && Uri.TryCreate(o.UsersBaseUrl, UriKind.Absolute, out _) && Uri.TryCreate(o.WalletBaseUrl, UriKind.Absolute, out _)
                && !string.IsNullOrWhiteSpace(o.ClientId) && !string.IsNullOrWhiteSpace(o.ClientSecret)),
                "Enabled registration delivery requires valid URLs, credentials, and bounded timing.");
        services.AddHttpClient<RegistrationDeliveryClient>((sp, http) =>
            http.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IOptions<RegistrationDeliveryOptions>>().Value.RequestTimeoutSeconds));
        services.AddHostedService<RegistrationDeliveryWorker>();
        return services;
    }
}


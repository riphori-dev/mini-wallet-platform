extern alias wallet;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using MiniWallet.Application.Interfaces;
using Xunit;

namespace MiniWallet.Tests;

public sealed partial class TransactionTests
{
    [PostgresFact]
    public async Task Notifications_follow_commit_skip_replays_and_rejections_and_cannot_fail_the_write()
    {
        var player = await Player();
        decimal? observedBalance = null;
        var notifications = new RecordingNotifications(async () => { observedBalance = await Balance(player); });
        await using var host = fixture.WalletHost().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBalanceNotifications>();
            services.AddSingleton<IBalanceNotifications>(notifications);
        }));
        using var provider = await Provider();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = provider.DefaultRequestHeaders.Authorization;
        var bet = Bet(player);
        await using (var db = fixture.Db())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION test_notification_rollback() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'injected failure' USING ERRCODE='40001'; END; $$;
                CREATE TRIGGER test_notification_rollback BEFORE UPDATE ON "TransactionOutcomes"
                FOR EACH ROW EXECUTE FUNCTION test_notification_rollback();
                """);
            try
            {
                Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, (await Send(client, bet)).StatusCode);
                Assert.Empty(notifications.Calls);
                Assert.Equal(100m, await Balance(player));
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync("""
                    DROP TRIGGER test_notification_rollback ON "TransactionOutcomes";
                    DROP FUNCTION test_notification_rollback();
                    """);
            }
        }
        Assert.Equal(System.Net.HttpStatusCode.Created, (await Send(client, bet)).StatusCode);
        Assert.Equal((player, "USD"), Assert.Single(notifications.Calls));
        Assert.Equal(90m, observedBalance);
        Assert.Equal(System.Net.HttpStatusCode.Created, (await Send(client, bet)).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, (await Send(client, Bet(player, amount: "1000"))).StatusCode);
        Assert.Single(notifications.Calls);
        notifications.Throw = true;
        Assert.Equal(System.Net.HttpStatusCode.Created, (await Send(client, Bet(player))).StatusCode);
        Assert.Equal(80m, await Balance(player));
        notifications.Throw = false;
        Assert.Equal(System.Net.HttpStatusCode.Created, (await Send(client, Win(player, bet.ExternalTransactionId))).StatusCode);
        Assert.Equal(3, notifications.Calls.Count);
        Assert.Equal(105m, observedBalance);
    }

    private sealed class RecordingNotifications(Func<Task> checkCommit) : IBalanceNotifications
    {
        public List<(Guid, string)> Calls { get; } = [];
        public bool Throw { get; set; }
        public async Task ChangedAsync(Guid playerId, string currency, CancellationToken ct)
        {
            Calls.Add((playerId, currency));
            if (Throw) throw new InvalidOperationException("Simulated notification failure");
            await checkCommit();
        }
    }
}

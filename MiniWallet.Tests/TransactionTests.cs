extern alias wallet;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MiniWallet.Application;
using MiniWallet.Domain.Entities;
using MiniWallet.Infrastructure;
using Npgsql;
using Xunit;

namespace MiniWallet.Tests;

public sealed partial class TransactionTests(ProvisioningFixture fixture) : IClassFixture<ProvisioningFixture>
{
    private static string Id() => Guid.NewGuid().ToString("N");
    private async Task<HttpClient> Provider(string? provider = null)
    {
        var client = fixture.Wallet.CreateClient();
        var token = provider is null ? await fixture.Token("local-provider", "local-provider-secret-123!", "transactions:write")
            : new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("miniwallet-local", "wallet-api",
                [new Claim("sub", provider), new("provider_id", provider), new("kind", "machine"), new("role", "provider"), new("scope", "transactions:write")],
                DateTime.UtcNow, DateTime.UtcNow.AddMinutes(10),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UsersDatabaseFixture.Key)), SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
    private async Task<Guid> Player(decimal funding = 100m)
    {
        var player = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations",
            new PlayerRegistered(Guid.NewGuid(), player, DateTimeOffset.UtcNow))).StatusCode);
        if (funding > 0) await Fund(player, Currency.Usd, funding);
        return player;
    }
    private async Task Fund(Guid player, Currency currency, decimal amount)
    {
        await using var db = fixture.Db();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var wallet = await db.Wallets.FromSqlInterpolated(
            $"SELECT * FROM \"Wallets\" WHERE \"PlayerId\"={player} AND \"Currency\"={currency.Code} FOR UPDATE").SingleAsync();
        var money = Money.Amount(currency, amount);
        wallet.OpeningCredit(money);
        db.Entries.Add(FinancialEntry.Record(wallet, "test-fixture", Id(), "DEV_FUNDING", money, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    private static TransactionRequest Bet(Guid player, string? id = null, string amount = "10.00", string currency = "USD") =>
        new(id ?? Id(), player, currency, "BET", amount);
    private static TransactionRequest Win(Guid player, string bet, string? id = null, string amount = "25.00", string currency = "USD") =>
        new(id ?? Id(), player, currency, "WIN", amount, bet);
    private static Task<HttpResponseMessage> Send(HttpClient client, TransactionRequest request) =>
        client.PostAsJsonAsync("/api/v1/transactions", request);
    private async Task<decimal> Balance(Guid player, string currency = "USD")
    {
        await using var db = fixture.Db();
        return await db.Wallets.Where(x => x.PlayerId == player && x.Currency == Currency.Parse(currency)).Select(x => x.Balance).SingleAsync();
    }

    [PostgresFact]
    public async Task Bet_win_and_exact_replay_return_original_response_across_hosts()
    {
        var player = await Player();
        using var client = await Provider();
        var bet = Bet(player);
        var first = await Send(client, bet);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var original = await first.Content.ReadAsStringAsync();
        Assert.Equal(90m, await Balance(player));
        var win = Win(player, bet.ExternalTransactionId);
        Assert.Equal(HttpStatusCode.Created, (await Send(client, win)).StatusCode);
        Assert.Equal(115m, await Balance(player));
        await using var restarted = fixture.WalletHost();
        using var replayClient = restarted.CreateClient();
        replayClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        var replay = await Send(replayClient, bet with { Amount = "10" });
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        Assert.Equal(115m, await Balance(player));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, bet with { Amount = "11.00" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, Win(player, bet.ExternalTransactionId))).StatusCode);
    }

    [PostgresFact]
    public async Task Identical_concurrent_requests_have_one_effect_and_one_original_response()
    {
        var player = await Player();
        using var first = await Provider();
        await using var otherHost = fixture.WalletHost();
        using var second = otherHost.CreateClient();
        second.DefaultRequestHeaders.Authorization = first.DefaultRequestHeaders.Authorization;
        var bet = Bet(player);
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Send(i % 2 == 0 ? first : second, bet)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var bodies = await Task.WhenAll(responses.Select(x => x.Content.ReadAsStringAsync()));
        Assert.Single(bodies.Distinct());
        Assert.Equal(90m, await Balance(player));
        await using var db = fixture.Db();
        Assert.Equal(1, await db.Entries.CountAsync(x => x.PlayerId == player && x.Type == "BET"));
    }

    [PostgresFact]
    public async Task Same_key_targeting_different_wallets_conflicts_before_second_effect()
    {
        var firstPlayer = await Player();
        var secondPlayer = await Player();
        using var provider = await Provider();
        var id = Id();
        var responses = await Task.WhenAll(Send(provider, Bet(firstPlayer, id)), Send(provider, Bet(secondPlayer, id)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(190m, await Balance(firstPlayer) + await Balance(secondPlayer));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(provider, Bet(firstPlayer, id, currency: "EUR"))).StatusCode);
    }

    [PostgresFact]
    public async Task Different_concurrent_bets_never_overdraw_and_rejections_are_terminal()
    {
        var player = await Player();
        using var provider = await Provider();
        var requests = Enumerable.Range(0, 12).Select(_ => Bet(player)).ToArray();
        var responses = await Task.WhenAll(requests.Select(x => Send(provider, x)));
        Assert.Equal(10, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
        Assert.Equal(2, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0m, await Balance(player));
        var rejected = Array.FindIndex(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var original = await responses[rejected].Content.ReadAsStringAsync();
        await Fund(player, Currency.Usd, 20m);
        var replay = await Send(provider, requests[rejected]);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        Assert.Equal(20m, await Balance(player));
    }

    [PostgresFact]
    public async Task Win_requires_accepted_matching_bet_and_one_settlement()
    {
        var player = await Player();
        var other = await Player();
        using var provider = await Provider();
        using var anotherProvider = await Provider("other-provider");
        var bet = Bet(player);
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, bet)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(provider, Win(player, "missing"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(anotherProvider, Win(player, bet.ExternalTransactionId))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(provider, Win(other, bet.ExternalTransactionId))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(provider, Win(player, bet.ExternalTransactionId, currency: "EUR"))).StatusCode);
        var settlements = await Task.WhenAll(Send(provider, Win(player, bet.ExternalTransactionId)),
            Send(provider, Win(player, bet.ExternalTransactionId)));
        Assert.Single(settlements, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(settlements, x => x.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(115m, await Balance(player));
        var tooLargeBet = Bet(player, amount: "1000.00");
        Assert.Equal(HttpStatusCode.Conflict, (await Send(provider, tooLargeBet)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(provider, Win(player, tooLargeBet.ExternalTransactionId))).StatusCode);
    }

    [PostgresFact]
    public async Task Validation_does_not_reserve_key_and_unprovisioned_wallet_remains_retryable()
    {
        var player = await Player();
        using var provider = await Provider();
        var request = Bet(player);
        foreach (var invalid in new[] { "0", "-1", "1.001", "1.000", "1e2", "100000000000000000000", "NaN" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(provider, request with { Amount = invalid })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await provider.PostAsJsonAsync("/api/v1/transactions",
            new { externalTransactionId = Id(), playerId = player, currency = "USD", type = "BET", amount = 1.00m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await provider.PostAsJsonAsync("/api/v1/transactions",
            new { externalTransactionId = Id(), playerId = player, currency = "USD", type = "BET", amount = "1.00", providerId = "spoof" })).StatusCode);
        var pendingPlayer = Guid.NewGuid();
        var pending = Bet(pendingPlayer, amount: "0.01");
        Assert.Equal(HttpStatusCode.NotFound, (await Send(provider, pending)).StatusCode);
        await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations", new PlayerRegistered(Guid.NewGuid(), pendingPlayer, DateTimeOffset.UtcNow));
        await Fund(pendingPlayer, Currency.Usd, 1m);
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, pending)).StatusCode);
    }

    [PostgresFact]
    public async Task Ledger_failure_rolls_back_balance_ledger_and_key_then_same_id_retries()
    {
        var player = await Player();
        using var provider = await Provider();
        var request = Bet(player);
        await using var db = fixture.Db();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_fail_outcome() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'transient injected failure' USING ERRCODE='40001'; END; $$;
            CREATE TRIGGER test_fail_outcome BEFORE UPDATE ON "TransactionOutcomes" FOR EACH ROW EXECUTE FUNCTION test_fail_outcome();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Send(provider, request)).StatusCode);
            Assert.Equal(100m, await Balance(player));
            Assert.False(await db.TransactionOutcomes.AnyAsync(x => x.ExternalTransactionId == request.ExternalTransactionId));
            Assert.False(await db.Entries.AnyAsync(x => x.ExternalTransactionId == request.ExternalTransactionId));
        }
        finally { await db.Database.ExecuteSqlRawAsync("""DROP TRIGGER test_fail_outcome ON "TransactionOutcomes"; DROP FUNCTION test_fail_outcome();"""); }
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, request)).StatusCode);
        Assert.Equal(90m, await Balance(player));
    }

    [PostgresFact]
    public async Task Lock_timeout_is_retryable_without_a_reserved_key()
    {
        var player = await Player();
        using var provider = await Provider();
        var request = Bet(player);
        await using var db = fixture.Db();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT * FROM \"Wallets\" WHERE \"PlayerId\"={player} AND \"Currency\"='USD' FOR UPDATE");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Send(provider, request)).StatusCode);
            await transaction.RollbackAsync();
        }
        Assert.False(await db.TransactionOutcomes.AnyAsync(x => x.ExternalTransactionId == request.ExternalTransactionId));
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, request)).StatusCode);
    }

    [PostgresFact]
    public async Task Database_rejects_bypass_precision_negative_balance_unmatched_win_and_history_mutation()
    {
        var player = await Player();
        using var provider = await Provider();
        var bet = Bet(player);
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, bet)).StatusCode);
        await using var db = fixture.Db();
        foreach (var invalid in new[] { -1m, 1.001m, 100000000000000000000m })
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Wallets\" SET \"Balance\"={invalid} WHERE \"PlayerId\"={player} AND \"Currency\"='USD'"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Wallets\" SET \"Balance\"=0.000000001 WHERE \"PlayerId\"={player} AND \"Currency\"='BTC'"));
        var walletEntity = await db.Wallets.SingleAsync(x => x.PlayerId == player && x.Currency == Currency.Usd);
        var acceptedBet = await db.Entries.SingleAsync(x => x.ExternalTransactionId == bet.ExternalTransactionId);
        db.Entries.Add(FinancialEntry.Record(walletEntity, "wrong-provider", Id(), "WIN", Money.Amount(Currency.Usd, 1m),
            DateTimeOffset.UtcNow, acceptedBet.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"FinancialEntries\" SET \"Amount\"=1 WHERE \"Id\"={acceptedBet.Id}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"TransactionOutcomes\" WHERE \"ExternalTransactionId\"={bet.ExternalTransactionId}"));
    }

    [PostgresFact]
    public async Task Owned_reads_admin_access_page_bounds_and_machine_isolation()
    {
        var email = await fixture.Users.VerifiedPlayer();
        await using var usersDb = fixture.Users.Db();
        var player = await usersDb.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations", new PlayerRegistered(Guid.NewGuid(), player, DateTimeOffset.UtcNow));
        await Fund(player, Currency.Usd, 100m);
        using var provider = await Provider();
        await Send(provider, Bet(player));
        await Send(provider, Bet(player));
        using var reader = fixture.Wallet.CreateClient();
        reader.DefaultRequestHeaders.Authorization = new("Bearer", await fixture.Users.Login(email, UsersDatabaseFixture.Password));
        var own = await reader.GetFromJsonAsync<PlayerBalances>("/api/v1/me/wallets?playerId=" + Guid.NewGuid());
        Assert.Equal(player, own!.PlayerId);
        Assert.Equal("80.00", own.Wallets.Single(x => x.Currency == "USD").Balance);
        var page1 = await reader.GetFromJsonAsync<HistoryPage>("/api/v1/me/wallets/USD/transactions?page=1&pageSize=2");
        var repeat = await reader.GetFromJsonAsync<HistoryPage>("/api/v1/me/wallets/USD/transactions?page=1&pageSize=2");
        var page2 = await reader.GetFromJsonAsync<HistoryPage>("/api/v1/me/wallets/USD/transactions?page=2&pageSize=2");
        Assert.True(page1!.HasMore);
        Assert.Equal(page1.Items.Select(x => x.TransactionId), repeat!.Items.Select(x => x.TransactionId));
        Assert.Empty(page1.Items.Select(x => x.TransactionId).Intersect(page2!.Items.Select(x => x.TransactionId)));
        Assert.Contains(page2.Items, x => x.Type == "DEV_FUNDING");
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.GetAsync("/api/v1/me/wallets/USD/transactions?page=0&pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/v1/players/{player}/wallets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(reader, Bet(player))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await provider.GetAsync("/api/v1/me/wallets")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await provider.GetAsync($"/api/v1/players/{player}/wallets")).StatusCode);
        reader.DefaultRequestHeaders.Authorization = new("Bearer", await fixture.Users.Login("admin@miniwallet.test", "Local-admin-password-123!"));
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/v1/players/{player}/wallets")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/v1/players/{player}/wallets/USD/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/players/{Guid.NewGuid()}/wallets")).StatusCode);
    }

    [PostgresFact]
    public async Task Currency_precision_and_upper_bound_are_enforced_without_silent_rounding()
    {
        var player = await Player(99999999999999999999.99m);
        using var provider = await Provider();
        var bet = Bet(player, amount: "1.00");
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, bet)).StatusCode);
        var overflow = await Send(provider, Win(player, bet.ExternalTransactionId, amount: "2.00"));
        Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode);
        Assert.Contains("balance_limit_exceeded", await overflow.Content.ReadAsStringAsync());
        Assert.Equal(99999999999999999998.99m, await Balance(player));
        await Fund(player, Currency.Btc, 0.00000002m);
        var btcBet = Bet(player, amount: "0.00000001", currency: "BTC");
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, btcBet)).StatusCode);
        var btcWin = await Send(provider, Win(player, btcBet.ExternalTransactionId, amount: "0.00000003", currency: "BTC"));
        Assert.Equal(HttpStatusCode.Created, btcWin.StatusCode);
        Assert.Contains("\"balanceAfter\":\"0.00000004\"", await btcWin.Content.ReadAsStringAsync());
    }

    [PostgresFact]
    public async Task Provider_namespace_is_distinct_and_unfinished_keys_cannot_commit()
    {
        var player = await Player();
        using var first = await Provider();
        using var second = await Provider("another-provider");
        var request = Bet(player);
        Assert.Equal(HttpStatusCode.Created, (await Send(first, request)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Send(second, request)).StatusCode);
        Assert.Equal(80m, await Balance(player));
        await using var db = fixture.Db();
        var key = Id();
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TransactionOutcomes" ("ProviderId","ExternalTransactionId","PlayerId","Currency","Type","Amount","Status")
            VALUES ('unfinished',{key},{player},'USD','BET',1,0)
            """));
        Assert.False(await db.TransactionOutcomes.AnyAsync(x => x.ExternalTransactionId == key));
    }

    [PostgresFact]
    public async Task Development_fixture_is_explicit_idempotent_and_new_players_are_zero()
    {
        await using (var db = fixture.Db()) await DevelopmentWalletFixture.SeedAsync(db);
        await using (var db = fixture.Db()) await DevelopmentWalletFixture.SeedAsync(db);
        Assert.Equal(100m, await Balance(DevelopmentWalletFixture.PlayerId));
        Assert.Equal(0.01m, await Balance(DevelopmentWalletFixture.PlayerId, "BTC"));
        await using var verify = fixture.Db();
        Assert.Equal(3, await verify.Entries.CountAsync(x => x.PlayerId == DevelopmentWalletFixture.PlayerId && x.Type == "DEV_FUNDING"));
        var player = await Player(0m);
        Assert.Equal(0m, await Balance(player));
    }
}


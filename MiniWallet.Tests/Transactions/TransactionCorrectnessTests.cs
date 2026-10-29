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
using MiniWallet.Infrastructure.Data;
using Npgsql;
using Xunit;

namespace MiniWallet.Tests;

public sealed partial class TransactionTests
{
    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal((int)status, root.GetProperty("status").GetInt32());
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal("urn:miniwallet:problem:" + code, root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(root.TryGetProperty("stackTrace", out _));
    }

    [PostgresFact]
    public async Task Two_wallet_hosts_racing_bets_of_80_against_100_leave_20()
    {
        var player = await Player();
        using var first = await Provider();
        await using var secondHost = fixture.WalletHost();
        using var second = secondHost.CreateClient();
        second.DefaultRequestHeaders.Authorization = first.DefaultRequestHeaders.Authorization;
        Assert.NotSame(fixture.Wallet.Services, secondHost.Services);
        var requests = new[] { Bet(player, amount: "80.00"), Bet(player, amount: "80.00") };
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Race(HttpClient host, TransactionRequest request)
        {
            await start.Task;
            return await Send(host, request);
        }
        var one = Race(first, requests[0]);
        var two = Race(second, requests[1]);
        start.SetResult();
        var responses = await Task.WhenAll(one, two);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        var rejected = Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await AssertProblem(rejected, HttpStatusCode.Conflict, "insufficient_funds");
        // Independent read context after the two host-scoped contexts/transactions committed.
        await using var verify = fixture.Db();
        Assert.Equal(20m, await verify.Wallets.Where(x => x.PlayerId == player && x.Currency == Currency.Usd).Select(x => x.Balance).SingleAsync());
        Assert.Equal(1, await verify.Entries.CountAsync(x => x.PlayerId == player && x.Type == "BET"));
        Assert.Equal(2, await verify.TransactionOutcomes.CountAsync(x => x.PlayerId == player));
        Assert.Equal(1, await verify.TransactionOutcomes.CountAsync(x => x.PlayerId == player && x.Status == 409));
    }

    [PostgresFact]
    public async Task Exact_balance_acceptance_then_insufficient_funds_preserves_entire_history()
    {
        var player = await Player();
        using var provider = await Provider();
        var exact = await Send(provider, Bet(player, amount: "100.00"));
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);
        using var success = JsonDocument.Parse(await exact.Content.ReadAsStringAsync());
        Assert.Equal("0.00", success.RootElement.GetProperty("balanceAfter").GetString());
        await using var before = fixture.Db();
        var history = await before.Entries.Where(x => x.PlayerId == player).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        var rejected = Bet(player, amount: "0.01");
        await AssertProblem(await Send(provider, rejected), HttpStatusCode.Conflict, "insufficient_funds");
        await using var after = fixture.Db();
        Assert.Equal(0m, await after.Wallets.Where(x => x.PlayerId == player && x.Currency == Currency.Usd).Select(x => x.Balance).SingleAsync());
        Assert.Equal(history, await after.Entries.Where(x => x.PlayerId == player).OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        Assert.False(await after.Entries.AnyAsync(x => x.ExternalTransactionId == rejected.ExternalTransactionId));
    }

    [PostgresFact]
    public async Task Player_A_cannot_read_B_and_actual_endpoints_enforce_token_failures()
    {
        var emailA = await fixture.Users.VerifiedPlayer();
        var emailB = await fixture.Users.VerifiedPlayer();
        await using var users = fixture.Users.Db();
        var a = await users.Users.Where(x => x.Email == emailA).Select(x => x.Id).SingleAsync();
        var b = await users.Users.Where(x => x.Email == emailB).Select(x => x.Id).SingleAsync();
        foreach (var id in new[] { a, b })
            Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations",
                new PlayerRegistered(Guid.NewGuid(), id, DateTimeOffset.UtcNow))).StatusCode);
        await Fund(a, Currency.Usd, 10m);
        await Fund(b, Currency.Usd, 200m);
        using var provider = await Provider();
        var privateBet = Bet(b);
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, privateBet)).StatusCode);
        using var reader = fixture.Wallet.CreateClient();
        await AssertProblem(await reader.GetAsync("/api/v1/me/wallets"), HttpStatusCode.Unauthorized, "unauthenticated");
        reader.DefaultRequestHeaders.Authorization = new("Bearer", "invalid");
        await AssertProblem(await reader.GetAsync("/api/v1/me/wallets"), HttpStatusCode.Unauthorized, "unauthenticated");
        var expired = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("miniwallet-local", "wallet-api",
            [new Claim("sub", a.ToString()), new("role", "player"), new("kind", "user"), new("email_verified", "true"), new("scope", "wallet:read")],
            DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UsersDatabaseFixture.Key)), SecurityAlgorithms.HmacSha256)));
        reader.DefaultRequestHeaders.Authorization = new("Bearer", expired);
        await AssertProblem(await reader.GetAsync("/api/v1/me/wallets"), HttpStatusCode.Unauthorized, "unauthenticated");

        reader.DefaultRequestHeaders.Authorization = new("Bearer", await fixture.Users.Login(emailA, UsersDatabaseFixture.Password));
        foreach (var path in new[] { $"/api/v1/players/{b}/wallets", $"/api/v1/players/{b}/wallets/USD/transactions" })
            await AssertProblem(await reader.GetAsync(path), HttpStatusCode.Forbidden, "forbidden");
        var mine = await reader.GetFromJsonAsync<PlayerBalances>("/api/v1/me/wallets?playerId=" + b);
        Assert.Equal(a, mine!.PlayerId);
        Assert.Equal("10.00", mine.Wallets.Single(x => x.Currency == "USD").Balance);
        var mineHistory = await reader.GetFromJsonAsync<HistoryPage>("/api/v1/me/wallets/USD/transactions?playerId=" + b);
        Assert.DoesNotContain(mineHistory!.Items, x => x.ExternalTransactionId == privateBet.ExternalTransactionId);
        await AssertProblem(await Send(reader, Bet(a)), HttpStatusCode.Forbidden, "forbidden");
        await AssertProblem(await provider.GetAsync("/api/v1/me/wallets"), HttpStatusCode.Forbidden, "forbidden");
        reader.DefaultRequestHeaders.Authorization = new("Bearer", await fixture.Users.Login("admin@miniwallet.test", "Local-admin-password-123!"));
        var other = await reader.GetFromJsonAsync<PlayerBalances>($"/api/v1/players/{b}/wallets");
        Assert.Equal(b, other!.PlayerId);
        Assert.Equal("190.00", other.Wallets.Single(x => x.Currency == "USD").Balance);
        var otherHistory = await reader.GetFromJsonAsync<HistoryPage>($"/api/v1/players/{b}/wallets/USD/transactions");
        Assert.Contains(otherHistory!.Items, x => x.ExternalTransactionId == privateBet.ExternalTransactionId);
    }

    [PostgresFact]
    public async Task Response_loss_after_commit_then_host_restart_replays_one_financial_effect()
    {
        var player = await Player();
        using var provider = await Provider();
        var request = Bet(player);
        await using (var firstHost = fixture.WalletHost())
        {
            using var bootstrap = firstHost.CreateClient(); // Start this independent host.
            using var lossy = new HttpClient(new DropCommittedResponse(firstHost.Server.CreateHandler())) { BaseAddress = new Uri("http://localhost") };
            lossy.DefaultRequestHeaders.Authorization = provider.DefaultRequestHeaders.Authorization;
            await Assert.ThrowsAsync<HttpRequestException>(() => Send(lossy, request));
        } // Stop/dispose the first host; no in-memory replay state survives.
        await using var verify = fixture.Db();
        var committed = await verify.TransactionOutcomes.AsNoTracking().SingleAsync(x => x.ExternalTransactionId == request.ExternalTransactionId);
        Assert.Equal(201, committed.Status);
        await using var restarted = fixture.WalletHost();
        using var retry = restarted.CreateClient();
        retry.DefaultRequestHeaders.Authorization = provider.DefaultRequestHeaders.Authorization;
        var replay = await Send(retry, request);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(committed.Response, await replay.Content.ReadAsStringAsync());
        Assert.Equal(90m, await Balance(player));
        Assert.Equal(1, await verify.Entries.CountAsync(x => x.PlayerId == player && x.Type == "BET"));
    }

    private sealed class DropCommittedResponse(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var response = await base.SendAsync(request, ct);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            throw new HttpRequestException("Simulated response loss after receiver committed.");
        }
    }

    [PostgresFact]
    public async Task Database_unique_settlement_survives_application_guard_bypass()
    {
        var player = await Player();
        using var provider = await Provider();
        var bet = Bet(player);
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, bet)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Send(provider, Win(player, bet.ExternalTransactionId))).StatusCode);
        await using var db = fixture.Db();
        var wallet = await db.Wallets.SingleAsync(x => x.PlayerId == player && x.Currency == Currency.Usd);
        var acceptedBet = await db.Entries.SingleAsync(x => x.ExternalTransactionId == bet.ExternalTransactionId);
        db.Entries.Add(FinancialEntry.Record(wallet, "local-provider", Id(), "WIN",
            Money.Amount(Currency.Usd, 1m), DateTimeOffset.UtcNow, acceptedBet.Id));
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(failure.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("IX_FinancialEntries_OriginalBetId", postgres.ConstraintName);
        await using var verify = fixture.Db();
        Assert.Equal(1, await verify.Entries.CountAsync(x => x.OriginalBetId == acceptedBet.Id));
        Assert.Equal(115m, await Balance(player));
    }

    [PostgresFact]
    public async Task Migrations_match_models_and_repeat_safely_on_independent_connections()
    {
        await using var first = fixture.Db();
        await using var second = fixture.Db();
        Assert.NotEqual(first.ContextId, second.ContextId);
        await Task.WhenAll(DatabaseSetup.MigrateAsync(first), DatabaseSetup.MigrateAsync(second));
        Assert.Empty(await first.Database.GetPendingMigrationsAsync());
        Assert.False(first.Database.HasPendingModelChanges());
        Assert.Contains(await first.Database.GetAppliedMigrationsAsync(), x => x.EndsWith("_FinancialGuards"));
        await using var users = fixture.Users.Db();
        Assert.Empty(await users.Database.GetPendingMigrationsAsync());
        Assert.False(users.Database.HasPendingModelChanges());
    }

    [PostgresFact]
    public async Task Unsupported_currency_and_page_error_contracts_do_not_reserve_keys()
    {
        var player = await Player();
        using var provider = await Provider();
        var invalid = Bet(player, currency: "JPY");
        await AssertProblem(await Send(provider, invalid), HttpStatusCode.BadRequest, "invalid_request");
        await using var db = fixture.Db();
        Assert.False(await db.TransactionOutcomes.AnyAsync(x => x.ExternalTransactionId == invalid.ExternalTransactionId));
        using var admin = fixture.Wallet.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", await fixture.Users.Login("admin@miniwallet.test", "Local-admin-password-123!"));
        foreach (var query in new[] { "page=0", "page=10001", "pageSize=0", "pageSize=101" })
            await AssertProblem(await admin.GetAsync($"/api/v1/players/{player}/wallets/USD/transactions?{query}"),
                HttpStatusCode.BadRequest, "invalid_request");
        var empty = await admin.GetFromJsonAsync<HistoryPage>($"/api/v1/players/{player}/wallets/USD/transactions?page=2&pageSize=1");
        Assert.Empty(empty!.Items);
        Assert.False(empty.HasMore);
        Assert.Equal(2, empty.Page);
        Assert.Equal(1, empty.PageSize);
        await AssertProblem(await admin.GetAsync($"/api/v1/players/{Guid.NewGuid()}/wallets"),
            HttpStatusCode.NotFound, "wallet_unavailable");
    }
}


extern alias wallet;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;
using Auditing = wallet::MiniWallet.Auditing;

namespace MiniWallet.Tests;

public class AuditTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unauthorized_requests_are_audited_without_secrets_and_sink_failure_preserves_response(bool fail)
    {
        var sink = new CaptureWriter(fail);
        await using var host = new WebApplicationFactory<wallet::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = "Host=localhost;Database=unused;Username=unused;Password=unused" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<Auditing.IAuditWriter>();
                services.AddSingleton<Auditing.IAuditWriter>(sink);
            });
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer secret-header-token");
        var response = await client.PostAsync("/api/v1/transactions?access_token=secret-query-token",
            new StringContent("{\"password\":\"secret-body-password\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        for (var i = 0; sink.Records.IsEmpty && i < 100; i++) await Task.Delay(10);
        var record = Assert.Single(sink.Records);
        Assert.Equal(401, record.Status);
        Assert.Equal("rejected", record.Outcome);
        Assert.Equal("Wallet", record.Service);
        Assert.Null(record.ActorId);
        Assert.Contains("transactions", record.Route);
        Assert.DoesNotContain("secret-", JsonSerializer.Serialize(record));
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(problem.RootElement.GetProperty("traceId").GetString(), record.TraceId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Single(sink.Records); // Probes are excluded.
    }

    private sealed class CaptureWriter(bool fail) : Auditing.IAuditWriter
    {
        public ConcurrentQueue<Auditing.AuditRecord> Records { get; } = new();
        public Task WriteAsync(Auditing.AuditRecord record, CancellationToken ct)
        {
            Records.Enqueue(record);
            return fail ? Task.FromException(new IOException("Simulated audit outage")) : Task.CompletedTask;
        }
    }
}

public sealed partial class TransactionTests
{
    [PostgresFact]
    public async Task Audit_outage_cannot_change_a_committed_bet_or_its_replay()
    {
        var player = await Player();
        await using var host = fixture.WalletHost().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<Auditing.IAuditWriter>();
            services.AddSingleton<Auditing.IAuditWriter, FailedAuditWriter>();
        }));
        using var provider = await Provider();
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = provider.DefaultRequestHeaders.Authorization;
        var bet = Bet(player);
        var accepted = await Send(client, bet);
        var replay = await Send(client, bet);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(await accepted.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(90m, await Balance(player));
    }

    private sealed class FailedAuditWriter : Auditing.IAuditWriter
    {
        public Task WriteAsync(Auditing.AuditRecord record, CancellationToken ct) =>
            Task.FromException(new IOException("Simulated audit database failure"));
    }

    [PostgresFact]
    public async Task Successful_user_login_is_audited_without_credentials_or_token()
    {
        var email = await fixture.Users.VerifiedPlayer();
        var token = await fixture.Users.Login(email, UsersDatabaseFixture.Password);
        var actorId = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Subject;
        string? record = null;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var connection = new NpgsqlConnection(fixture.Users.Connection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT "Record"::text FROM "AuditEvents"
                WHERE "Record"->>'ActorId' = @actor AND "Record"->>'Route' = 'dev/token'
                LIMIT 1
                """, connection);
            command.Parameters.AddWithValue("actor", actorId);
            record = await command.ExecuteScalarAsync() as string;
            if (record is not null) break;
            await Task.Delay(20);
        }
        Assert.NotNull(record);
        Assert.DoesNotContain(email, record);
        Assert.DoesNotContain(UsersDatabaseFixture.Password, record);
        Assert.DoesNotContain(token, record);
        using var parsed = JsonDocument.Parse(record);
        Assert.Equal(200, parsed.RootElement.GetProperty("Status").GetInt32());
        Assert.Equal("Users", parsed.RootElement.GetProperty("Service").GetString());
    }

    [PostgresFact]
    public async Task Durable_audit_distinguishes_committed_transaction_replay_and_business_rejection()
    {
        var player = await Player();
        using var client = await Provider();
        var bet = Bet(player);
        Assert.Equal(HttpStatusCode.Created, (await Send(client, bet)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Send(client, bet)).StatusCode);
        var rejected = Bet(player, amount: "1000.00");
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, rejected)).StatusCode);
        var rows = new List<JsonElement>();
        // Response bytes may arrive before the audit write completes.
        for (var attempt = 0; attempt < 100; attempt++)
        {
            rows.Clear();
            await using var connection = new NpgsqlConnection(fixture.Connection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT "Record"::text FROM "AuditEvents"
                WHERE "Record"->'Details'->>'ExternalTransactionId' IN (@bet, @rejected)
                """, connection);
            command.Parameters.AddWithValue("bet", bet.ExternalTransactionId);
            command.Parameters.AddWithValue("rejected", rejected.ExternalTransactionId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(JsonDocument.Parse(reader.GetString(0)).RootElement.Clone());
            if (rows.Count == 3) break;
            await Task.Delay(20);
        }
        Assert.Equal(3, rows.Count);
        Assert.Single(rows, row => row.GetProperty("Status").GetInt32() == 201 && row.GetProperty("Details").GetProperty("IsReplay").GetString() == "False");
        Assert.Single(rows, row => row.GetProperty("Status").GetInt32() == 201 && row.GetProperty("Details").GetProperty("IsReplay").GetString() == "True");
        Assert.Single(rows, row => row.GetProperty("Status").GetInt32() == 409);
        Assert.All(rows, row => Assert.Equal(player.ToString(), row.GetProperty("Details").GetProperty("TargetPlayerId").GetString()));
        Assert.Equal(90m, await Balance(player));
    }
}

extern alias users;
extern alias wallet;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniWallet.Application;
using MiniWallet.Infrastructure;
using MiniWallet.Users.Infrastructure;
using Npgsql;
using Xunit;

namespace MiniWallet.Tests;

public sealed class ProvisioningFixture : IAsyncLifetime
{
    public UsersDatabaseFixture Users { get; } = new();
    public string Connection { get; private set; } = "";
    public WebApplicationFactory<wallet::Program> Wallet { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    private readonly string database = "miniwallet_test_" + Guid.NewGuid().ToString("N");
    private string? admin;
    public WalletDbContext Db() => new(new DbContextOptionsBuilder<WalletDbContext>().UseNpgsql(Connection).Options);

    public async Task InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("TestPostgres");
        if (string.IsNullOrWhiteSpace(admin)) return;
        await Users.InitializeAsync();
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\" OWNER wallet_app", connection);
            await command.ExecuteNonQueryAsync();
        }
        var builder = new NpgsqlConnectionStringBuilder(admin) { Database = database, Username = "wallet_app", Password = "wallet_local_only" };
        Connection = builder.ConnectionString;
        await using (var db = Db()) await MiniWallet.Infrastructure.DatabaseSetup.MigrateAsync(db);
        Wallet = WalletHost();
        Client = Wallet.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new("Bearer", await Token("users-delivery", "local-internal-secret-123!", "players:provision"));
    }

    public WebApplicationFactory<wallet::Program> WalletHost() =>
        new WebApplicationFactory<wallet::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Database"] = Connection }));
        });

    public WebApplicationFactory<users::Program> UsersHost() =>
        new WebApplicationFactory<users::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = Users.Connection, ["Delivery:Enabled"] = "false" }));
        });

    public async Task<string> Token(string clientId, string clientSecret, string scope)
    {
        var response = await Users.Client.PostAsJsonAsync("/dev/connect/token", new { clientId, clientSecret, scope });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("access_token").GetString()!;
    }

    public RegistrationDeliveryWorker Worker(IServiceProvider provider, RoutingDeliveryHandler handler)
    {
        var options = Options.Create(new RegistrationDeliveryOptions
        {
            Enabled = true,
            UsersBaseUrl = "http://users.test",
            WalletBaseUrl = "http://wallet.test",
            ClientId = "users-delivery",
            ClientSecret = "local-internal-secret-123!",
            RequestTimeoutSeconds = 1,
            RetryDelaySeconds = 1
        });
        var client = new RegistrationDeliveryClient(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(1) }, options);
        return new(provider.GetRequiredService<IServiceScopeFactory>(), client, options, TimeProvider.System,
            NullLogger<RegistrationDeliveryWorker>.Instance);
    }

    public async Task RetryNow(Guid eventId)
    {
        await using var db = Users.Db();
        await db.RegistrationDeliveries.Where(x => x.EventId == eventId).ExecuteUpdateAsync(set =>
            set.SetProperty(x => x.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
    }
    public async Task<PlayerRegistrationDelivery> Delivery(string email)
    {
        await using var db = Users.Db();
        var id = await db.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        return await db.RegistrationDeliveries.AsNoTracking().SingleAsync(x => x.PlayerId == id);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(admin)) return;
        Client.Dispose();
        await Wallet.DisposeAsync();
        await Users.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class RoutingDeliveryHandler(ProvisioningFixture fixture) : HttpMessageHandler
{
    public bool Down { get; set; }
    public bool LoseAcknowledgement { get; set; }
    public bool Timeout { get; set; }
    public List<Guid> EventIds { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = await request.Content!.ReadAsByteArrayAsync(ct);
        using var clone = new HttpRequestMessage(request.Method, request.RequestUri!.PathAndQuery) { Content = new ByteArrayContent(body) };
        foreach (var header in request.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var header in request.Content.Headers) clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (request.RequestUri.Host == "users.test") return await fixture.Users.Client.SendAsync(clone, ct);
        using var json = JsonDocument.Parse(body);
        EventIds.Add(json.RootElement.GetProperty("eventId").GetGuid());
        if (Timeout) { await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, ct); }
        if (Down) return new(HttpStatusCode.ServiceUnavailable);
        var response = await fixture.Client.SendAsync(clone, ct);
        if (LoseAcknowledgement)
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            response.Dispose();
            throw new HttpRequestException("Receiver committed; acknowledgement lost.");
        }
        return response;
    }
}

public sealed class ProvisioningTests(ProvisioningFixture fixture) : IClassFixture<ProvisioningFixture>
{
    private static PlayerRegistered Event(Guid? player = null) => new(Guid.NewGuid(), player ?? Guid.NewGuid(), DateTimeOffset.UtcNow);

    [PostgresFact]
    public async Task Concurrent_replay_across_hosts_creates_exactly_three_zero_wallets()
    {
        var registration = Event();
        await using var secondHost = fixture.WalletHost();
        using var second = secondHost.CreateClient();
        second.DefaultRequestHeaders.Authorization = fixture.Client.DefaultRequestHeaders.Authorization;
        var responses = await Task.WhenAll(fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations", registration),
            second.PostAsJsonAsync("/internal/v1/player-registrations", registration));
        Assert.All(responses, x => Assert.Equal(HttpStatusCode.NoContent, x.StatusCode));
        await using var db = fixture.Db();
        var wallets = await db.Wallets.Where(x => x.PlayerId == registration.PlayerId).ToListAsync();
        Assert.Equal(new[] { "BTC", "EUR", "USD" }, wallets.Select(x => x.Currency.Code).Order().ToArray());
        Assert.All(wallets, x => Assert.Equal(0m, x.Balance));
        Assert.Equal(1, await db.RegistrationReceipts.CountAsync(x => x.EventId == registration.EventId));
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations",
            registration with { PlayerId = Guid.NewGuid() })).StatusCode);
    }

    [PostgresFact]
    public async Task Distinct_events_for_same_player_do_not_reset_existing_balance()
    {
        var registration = Event();
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations", registration)).StatusCode);
        await using var db = fixture.Db();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Wallets\" SET \"Balance\" = 7 WHERE \"PlayerId\" = {registration.PlayerId} AND \"Currency\" = 'USD'");
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations",
            registration with { EventId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(3, await db.Wallets.CountAsync(x => x.PlayerId == registration.PlayerId));
        Assert.Equal(7m, await db.Wallets.Where(x => x.PlayerId == registration.PlayerId && x.Currency == MiniWallet.Domain.Currency.Usd).Select(x => x.Balance).SingleAsync());
    }

    [PostgresFact]
    public async Task Partial_failure_rolls_back_receipt_and_all_wallets_then_retry_succeeds()
    {
        var registration = Event();
        await using var db = fixture.Db();
        // A real database fault after the USD insert, confined to this disposable database.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_fail_eur() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF NEW."Currency" = 'EUR' THEN RAISE EXCEPTION 'injected EUR insert failure'; END IF;
              RETURN NEW;
            END; $$;
            CREATE TRIGGER test_fail_eur BEFORE INSERT ON "Wallets" FOR EACH ROW EXECUTE FUNCTION test_fail_eur();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations", registration)).StatusCode);
            Assert.False(await db.RegistrationReceipts.AnyAsync(x => x.EventId == registration.EventId));
            Assert.False(await db.Wallets.AnyAsync(x => x.PlayerId == registration.PlayerId));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("""DROP TRIGGER test_fail_eur ON "Wallets"; DROP FUNCTION test_fail_eur();""");
        }
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations", registration)).StatusCode);
        Assert.Equal(3, await db.Wallets.CountAsync(x => x.PlayerId == registration.PlayerId));
    }

    [PostgresFact]
    public async Task Unauthorized_or_invalid_events_are_not_accepted()
    {
        var registration = Event();
        using var client = fixture.Wallet.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/internal/v1/player-registrations", registration)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", await fixture.Token("local-provider", "local-provider-secret-123!", "transactions:write"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/internal/v1/player-registrations", registration)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations",
            registration with { Version = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/internal/v1/player-registrations",
            registration with { PlayerId = Guid.Empty })).StatusCode);
        await using var db = fixture.Db();
        Assert.False(await db.RegistrationReceipts.AnyAsync(x => x.EventId == registration.EventId));
    }

    [PostgresFact]
    public async Task Normal_registration_is_delivered_automatically_by_worker_dispatch()
    {
        var email = await fixture.Users.Register();
        var delivery = await fixture.Delivery(email);
        using var router = new RoutingDeliveryHandler(fixture);
        using var worker = fixture.Worker(fixture.Users.Users.Services, router);
        await worker.DispatchOnceAsync(default);
        Assert.NotNull((await fixture.Delivery(email)).DeliveredAt);
        await using var db = fixture.Db();
        var wallets = await db.Wallets.Where(x => x.PlayerId == delivery.PlayerId).ToListAsync();
        Assert.Equal(3, wallets.Count);
        Assert.All(wallets, x => Assert.Equal(0m, x.Balance));
    }

    [PostgresFact]
    public async Task Wallet_outage_then_new_Users_host_recovers_pending_work()
    {
        var email = await fixture.Users.Register();
        var delivery = await fixture.Delivery(email);
        await using (var firstHost = fixture.UsersHost())
        {
            using var firstClient = firstHost.CreateClient();
            using var router = new RoutingDeliveryHandler(fixture) { Down = true };
            using var worker = fixture.Worker(firstHost.Services, router);
            await worker.DispatchOnceAsync(default);
        }
        Assert.Null((await fixture.Delivery(email)).DeliveredAt);
        Assert.Equal(1, (await fixture.Delivery(email)).Attempts);
        await fixture.RetryNow(delivery.EventId);
        await using (var restartedHost = fixture.UsersHost())
        {
            using var secondClient = restartedHost.CreateClient();
            using var router = new RoutingDeliveryHandler(fixture);
            using var worker = fixture.Worker(restartedHost.Services, router);
            await worker.DispatchOnceAsync(default);
        }
        Assert.NotNull((await fixture.Delivery(email)).DeliveredAt);
        await using var db = fixture.Db();
        Assert.Equal(3, await db.Wallets.CountAsync(x => x.PlayerId == delivery.PlayerId));
    }

    [PostgresFact]
    public async Task Receiver_commit_lost_acknowledgement_retries_same_event_without_duplicates()
    {
        var email = await fixture.Users.Register();
        var delivery = await fixture.Delivery(email);
        using var router = new RoutingDeliveryHandler(fixture) { LoseAcknowledgement = true };
        using var worker = fixture.Worker(fixture.Users.Users.Services, router);
        await worker.DispatchOnceAsync(default);
        Assert.Null((await fixture.Delivery(email)).DeliveredAt);
        await using var db = fixture.Db();
        Assert.Equal(3, await db.Wallets.CountAsync(x => x.PlayerId == delivery.PlayerId));
        Assert.Equal(1, await db.RegistrationReceipts.CountAsync(x => x.EventId == delivery.EventId));
        router.LoseAcknowledgement = false;
        await fixture.RetryNow(delivery.EventId);
        await worker.DispatchOnceAsync(default);
        Assert.NotNull((await fixture.Delivery(email)).DeliveredAt);
        Assert.Equal(2, router.EventIds.Count(x => x == delivery.EventId));
        Assert.Equal(3, await db.Wallets.CountAsync(x => x.PlayerId == delivery.PlayerId));
    }

    [PostgresFact]
    public async Task Bounded_timeout_retains_work_and_retry_delay_prevents_immediate_resend()
    {
        var email = await fixture.Users.Register();
        var delivery = await fixture.Delivery(email);
        using var router = new RoutingDeliveryHandler(fixture) { Timeout = true };
        using var worker = fixture.Worker(fixture.Users.Users.Services, router);
        await worker.DispatchOnceAsync(default);
        var failed = await fixture.Delivery(email);
        Assert.Null(failed.DeliveredAt);
        Assert.Equal("HTTP timeout", failed.LastError);
        router.Timeout = false;
        await worker.DispatchOnceAsync(default);
        Assert.Equal(1, router.EventIds.Count(x => x == delivery.EventId));
        await fixture.RetryNow(delivery.EventId);
        await worker.DispatchOnceAsync(default);
        Assert.NotNull((await fixture.Delivery(email)).DeliveredAt);
    }
}


extern alias users;
extern alias wallet;
using MiniWallet.Users.Infrastructure.Users;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MiniWallet.Users.Application;
using MiniWallet.Users.Infrastructure;
using Npgsql;
using Xunit;

namespace MiniWallet.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TestPostgres")))
            Skip = "Set TestPostgres or use the documented Compose test runner for real PostgreSQL tests.";
    }
}

public sealed class SwitchableSender(DevelopmentMailbox mailbox) : IEmailSender
{
    public bool Fail { get; set; }
    public bool Throw { get; set; }
    public Task<bool> SendVerificationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct)
    {
        if (Throw) throw new IOException("Simulated delivery failure.");
        return Fail ? Task.FromResult(false) : mailbox.SendVerificationAsync(email, token, expiresAt, ct);
    }
}

public sealed class UsersDatabaseFixture : IAsyncLifetime
{
    public const string Password = "Player-password-123!";
    public const string Key = "LOCAL-ONLY-miniwallet-signing-key-DO-NOT-DEPLOY-2026";
    private readonly string database = "miniwallet_test_" + Guid.NewGuid().ToString("N");
    private string? admin;
    public string Connection { get; private set; } = "";
    public WebApplicationFactory<users::Program> Users { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public DevelopmentMailbox Mailbox => Users.Services.GetRequiredService<DevelopmentMailbox>();
    public SwitchableSender Sender => Users.Services.GetRequiredService<SwitchableSender>();
    public UsersDbContext Db() => new(new DbContextOptionsBuilder<UsersDbContext>().UseNpgsql(Connection).Options);

    public async Task InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("TestPostgres");
        if (string.IsNullOrWhiteSpace(admin)) return;
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\" OWNER users_app", connection);
            await command.ExecuteNonQueryAsync();
        }
        var builder = new NpgsqlConnectionStringBuilder(admin)
        {
            Database = database,
            Username = "users_app",
            Password = "users_local_only"
        };
        Connection = builder.ConnectionString;
        await using (var db = Db())
        {
            await MiniWallet.Users.Infrastructure.DatabaseSetup.MigrateAsync(db);
            await DevelopmentIdentitySeeder.SeedAdminAsync(db, new PasswordService(), "admin@miniwallet.test", "Local-admin-password-123!");
        }
        Users = new WebApplicationFactory<users::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Database"] = Connection, ["Delivery:Enabled"] = "false" }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<SwitchableSender>();
                services.Replace(ServiceDescriptor.Singleton<IEmailSender>(sp => sp.GetRequiredService<SwitchableSender>()));
            });
        });
        Client = Users.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(admin)) return;
        Client.Dispose();
        await Users.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        // Only the randomly named database created by this fixture is removed.
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string> Register(string? email = null)
    {
        email ??= $"p-{Guid.NewGuid():N}@example.test";
        var response = await Client.PostAsJsonAsync("/api/v1/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return email;
    }
    public async Task<string> VerifiedPlayer()
    {
        var email = await Register();
        var message = Mailbox.Read().Last(x => x.Email == email);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync("/api/v1/email-verifications", new { message.Token })).StatusCode);
        return email;
    }
    public async Task<string> Login(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/dev/token", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("access_token").GetString()!;
    }
}

public sealed partial class UsersIntegrationTests(UsersDatabaseFixture fixture) : IClassFixture<UsersDatabaseFixture>
{
    [PostgresFact]
    public async Task Registration_commits_user_and_delivery_and_normalized_duplicate_is_conflict()
    {
        var email = await fixture.Register();
        await using var db = fixture.Db();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        Assert.False(user.EmailVerified);
        Assert.NotEqual(UsersDatabaseFixture.Password, user.PasswordHash);
        Assert.Single(await db.RegistrationDeliveries.Where(x => x.PlayerId == user.Id && x.DeliveredAt == null).ToListAsync());
        var token = fixture.Mailbox.Read().Last(x => x.Email == email).Token;
        Assert.NotEqual(token, user.VerificationTokenHash);
        Assert.Equal(new VerificationSecrets().Hash(token), user.VerificationTokenHash);
        var duplicate = await fixture.Client.PostAsJsonAsync("/api/v1/register",
            new { email = " " + email.ToUpperInvariant() + " ", password = UsersDatabaseFixture.Password });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(1, await db.Users.CountAsync(x => x.Email == email));
        Assert.Equal(1, await db.RegistrationDeliveries.CountAsync(x => x.PlayerId == user.Id));
    }

    [PostgresFact]
    public async Task Concurrent_duplicate_registrations_have_one_durable_effect()
    {
        var email = $"race-{Guid.NewGuid():N}@example.test";
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            fixture.Client.PostAsJsonAsync("/api/v1/register", new { email, password = UsersDatabaseFixture.Password })));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = fixture.Db();
        var id = await db.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        Assert.Equal(1, await db.RegistrationDeliveries.CountAsync(x => x.PlayerId == id));
    }

    [PostgresFact]
    public async Task Failed_outbox_insert_rolls_back_registration()
    {
        await using var db = fixture.Db();
        var user = MiniWallet.Users.Domain.Entities.User.Register(
            MiniWallet.Users.Domain.EmailAddress.Parse($"rollback-{Guid.NewGuid():N}@example.test"), "hash");
        var deliveryId = Guid.NewGuid();
        db.RegistrationDeliveries.Add(new() { EventId = deliveryId, PlayerId = user.Id, OccurredAt = DateTimeOffset.UtcNow });
        // Pretracked duplicate player delivery forces SaveChanges to reject the combined unit of work.
        await Assert.ThrowsAsync<DbUpdateException>(() => new UserStore(db).RegisterAsync(user, DateTimeOffset.UtcNow, default));
        await using var verify = fixture.Db();
        Assert.False(await verify.Users.AnyAsync(x => x.Id == user.Id));
        Assert.False(await verify.RegistrationDeliveries.AnyAsync(x => x.PlayerId == user.Id));
    }

    [PostgresFact]
    public async Task Invalid_input_and_escalation_fields_are_rejected_without_registration()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/register", new { email = "bad", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
        Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/v1/register",
            new { email = "escalate@example.test", password = UsersDatabaseFixture.Password, role = "admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/dev/token",
            new { email = "admin@miniwallet.test", password = "Local-admin-password-123!", sub = Guid.NewGuid(), role = "provider" })).StatusCode);
    }

    [PostgresFact]
    public async Task Verification_is_atomic_and_cannot_be_reused()
    {
        var email = await fixture.Register();
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync("/dev/token",
            new { email, password = UsersDatabaseFixture.Password })).StatusCode);
        var token = fixture.Mailbox.Read().Last(x => x.Email == email).Token;
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            fixture.Client.PostAsJsonAsync("/api/v1/email-verifications", new { token })));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications", new { token })).StatusCode);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(await fixture.Login(email, UsersDatabaseFixture.Password));
        await using var db = fixture.Db();
        Assert.Equal((await db.Users.SingleAsync(x => x.Email == email)).Id.ToString(), jwt.Subject);
        Assert.Contains(jwt.Claims, x => x.Type == "role" && x.Value == "player");
        Assert.InRange(jwt.ValidTo - jwt.ValidFrom, TimeSpan.FromMinutes(9), TimeSpan.FromMinutes(10));
    }

    [PostgresFact]
    public async Task Expired_verification_and_authenticated_resend_recovery()
    {
        var email = await fixture.Register();
        var old = fixture.Mailbox.Read().Last(x => x.Email == email).Token;
        await using (var db = fixture.Db())
            await db.Users.Where(x => x.Email == email).ExecuteUpdateAsync(update =>
                update.SetProperty(x => x.VerificationExpiresAt, DateTimeOffset.UtcNow.AddDays(-1)));
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications", new { token = old })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications/resend",
            new { email, password = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications/resend",
            new { email, password = UsersDatabaseFixture.Password })).StatusCode);
        var fresh = fixture.Mailbox.Read().Last(x => x.Email == email).Token;
        Assert.NotEqual(old, fresh);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications", new { token = old })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications", new { token = fresh })).StatusCode);
    }

    [PostgresFact]
    public async Task Email_failure_preserves_registration_and_resend_recovers()
    {
        fixture.Sender.Throw = true;
        string email;
        try { email = await fixture.Register(); }
        finally { fixture.Sender.Throw = false; }
        fixture.Sender.Fail = true;
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications/resend",
                new { email, password = UsersDatabaseFixture.Password })).StatusCode);
        }
        finally { fixture.Sender.Fail = false; }
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications/resend",
            new { email, password = UsersDatabaseFixture.Password })).StatusCode);
        var token = fixture.Mailbox.Read().Last(x => x.Email == email).Token;
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsJsonAsync("/api/v1/email-verifications", new { token })).StatusCode);
    }

    [PostgresFact]
    public async Task Wrong_credentials_mailbox_protection_and_separate_machine_flow()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/dev/token",
            new { email = "admin@miniwallet.test", password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/dev/token",
            new { email = "local-provider@example.test", password = "local-provider-secret-123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/dev/mailbox")).StatusCode);
        using var mailbox = new HttpRequestMessage(HttpMethod.Get, "/dev/mailbox");
        mailbox.Headers.Add("X-Dev-Mailbox-Key", "local-mailbox-key");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.SendAsync(mailbox)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/dev/connect/token",
            new { clientId = "local-provider", clientSecret = "wrong", scope = "transactions:write" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PostAsJsonAsync("/dev/connect/token",
            new { clientId = "local-provider", clientSecret = "local-provider-secret-123!", scope = "players:provision" })).StatusCode);
    }

    [PostgresFact]
    public async Task Wallet_enforces_roles_scopes_and_internal_audience()
    {
        await using var walletFactory = new WebApplicationFactory<wallet::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Database"] = "Host=unused;Database=unused" }));
            builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(PolicyProbeController).Assembly));
        });
        using var client = walletFactory.CreateClient();
        var player = await fixture.Login(await fixture.VerifiedPlayer(), UsersDatabaseFixture.Password);
        var admin = await fixture.Login("admin@miniwallet.test", "Local-admin-password-123!");
        async Task<string> Machine(string id, string secret, string scope)
        {
            var response = await fixture.Client.PostAsJsonAsync("/dev/connect/token", new { clientId = id, clientSecret = secret, scope });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("access_token").GetString()!;
        }
        var provider = await Machine("local-provider", "local-provider-secret-123!", "transactions:write");
        var internalToken = await Machine("users-delivery", "local-internal-secret-123!", "players:provision");
        async Task Expect(string path, string? token, HttpStatusCode status)
        {
            client.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(status, (await client.GetAsync("/test/" + path)).StatusCode);
        }
        await Expect("player", player, HttpStatusCode.OK);
        await Expect("admin", player, HttpStatusCode.Forbidden);
        await Expect("provider", player, HttpStatusCode.Forbidden);
        await Expect("internal", player, HttpStatusCode.Unauthorized);
        await Expect("admin", admin, HttpStatusCode.OK);
        await Expect("provider", admin, HttpStatusCode.Forbidden);
        await Expect("provider", provider, HttpStatusCode.OK);
        await Expect("player", provider, HttpStatusCode.Forbidden);
        await Expect("internal", provider, HttpStatusCode.Unauthorized);
        await Expect("internal", admin, HttpStatusCode.Unauthorized);
        await Expect("internal", internalToken, HttpStatusCode.OK);
        await Expect("provider", internalToken, HttpStatusCode.Unauthorized);
        await Expect("player", null, HttpStatusCode.Unauthorized);
        await Expect("provider", provider[..^5] + "aaaaa", HttpStatusCode.Unauthorized);

        string InvalidToken(string issuer, string audience, DateTime expires, string key)
        {
            var claims = new[] { new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", "player"),
                new Claim("kind", "user"), new Claim("scope", "wallet:read"), new Claim("email_verified", "true") };
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience, claims,
                expires.AddMinutes(-10), expires,
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));
        }
        await Expect("player", InvalidToken("wrong", "wallet-api", DateTime.UtcNow.AddMinutes(10), UsersDatabaseFixture.Key), HttpStatusCode.Unauthorized);
        await Expect("player", InvalidToken("miniwallet-local", "wrong", DateTime.UtcNow.AddMinutes(10), UsersDatabaseFixture.Key), HttpStatusCode.Unauthorized);
        await Expect("player", InvalidToken("miniwallet-local", "wallet-api", DateTime.UtcNow.AddMinutes(-1), UsersDatabaseFixture.Key), HttpStatusCode.Unauthorized);
        await Expect("player", InvalidToken("miniwallet-local", "wallet-api", DateTime.UtcNow.AddMinutes(10), "wrong-key-with-at-least-thirty-two-bytes!"), HttpStatusCode.Unauthorized);
    }

    [PostgresFact]
    public async Task Development_endpoints_are_unavailable_in_production()
    {
        await using var factory = new WebApplicationFactory<users::Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Database"] = fixture.Connection }));
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/dev/token",
            new { email = "admin@miniwallet.test", password = "Local-admin-password-123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/dev/connect/token",
            new { clientId = "local-provider", clientSecret = "local-provider-secret-123!", scope = "transactions:write" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/dev/mailbox")).StatusCode);
    }
}

// These endpoints exist only when this test assembly is explicitly added to the test host.
[ApiController]
public sealed class PolicyProbeController : ControllerBase
{
    [Authorize(Policy = "PlayerRead"), HttpGet("/test/player")] public IActionResult Player() => Ok();
    [Authorize(Policy = "AdminRead"), HttpGet("/test/admin")] public IActionResult Admin() => Ok();
    [Authorize(Policy = "ProviderWrite"), HttpGet("/test/provider")] public IActionResult Provider() => Ok();
    [Authorize(Policy = "InternalProvisioning"), HttpGet("/test/internal")] public IActionResult Internal() => Ok();
}


extern alias users;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniWallet.Users.Application;
using MiniWallet.Users.Infrastructure;
using Xunit;

namespace MiniWallet.Tests;

public sealed class SmtpPostgresFactAttribute : FactAttribute
{
    public SmtpPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TestPostgres"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TestMailpitHttpUrl")))
            Skip = "Requires TestPostgres and TestMailpitHttpUrl; use the Compose test runner.";
    }
}

public sealed partial class UsersIntegrationTests
{
    [SmtpPostgresFact]
    public async Task SMTP_registration_verifies_using_actual_Mailpit_email()
    {
        var settings = MailpitSettings();
        await using var host = SmtpHost(settings);
        using var client = host.CreateClient();
        var email = $"smtp-{Guid.NewGuid():N}@example.test";
        var registration = await client.PostAsJsonAsync("/api/v1/register", new { email, password = UsersDatabaseFixture.Password });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var result = await registration.Content.ReadFromJsonAsync<RegistrationResult>();
        Assert.Equal("sent", result!.VerificationDeliveryStatus);
        var token = await MailpitToken(email);
        await using var db = fixture.Db();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        Assert.NotEqual(token, user.VerificationTokenHash);
        Assert.Equal(new VerificationSecrets().Hash(token), user.VerificationTokenHash);
        Assert.True(await db.RegistrationDeliveries.AnyAsync(x => x.PlayerId == user.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/email-verifications", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/email-verifications", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/dev/token", new { email, password = UsersDatabaseFixture.Password })).StatusCode);
    }

    [SmtpPostgresFact]
    public async Task SMTP_outage_keeps_registration_and_resend_recovers_after_recovery()
    {
        var settings = MailpitSettings();
        var workingPort = settings.Port;
        var unused = new TcpListener(IPAddress.Loopback, 0);
        unused.Start();
        settings.Port = ((IPEndPoint)unused.LocalEndpoint).Port;
        unused.Stop();
        await using var host = SmtpHost(settings);
        using var client = host.CreateClient();
        var email = $"smtp-retry-{Guid.NewGuid():N}@example.test";
        var request = new { email, password = UsersDatabaseFixture.Password };
        var registration = await client.PostAsJsonAsync("/api/v1/register", request);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.Equal("unavailable", (await registration.Content.ReadFromJsonAsync<RegistrationResult>())!.VerificationDeliveryStatus);
        await using var db = fixture.Db();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Email == email);
        Assert.True(await db.RegistrationDeliveries.AnyAsync(x => x.PlayerId == user.Id));
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync("/api/v1/email-verifications/resend", request)).StatusCode);
        settings.Port = workingPort;
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/v1/email-verifications/resend", request)).StatusCode);
        var token = await MailpitToken(email);
        var replaced = await db.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id);
        Assert.NotEqual(user.VerificationTokenHash, replaced.VerificationTokenHash);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/v1/email-verifications", new { token })).StatusCode);
        Assert.Equal(1, await db.Users.CountAsync(x => x.Email == email));
        Assert.Equal(1, await db.RegistrationDeliveries.CountAsync(x => x.PlayerId == user.Id));
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<users::Program> SmtpHost(SmtpOptions settings) =>
        fixture.Users.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IEmailSender>(new SmtpEmailSender(
                Options.Create(settings), NullLogger<SmtpEmailSender>.Instance)))));

    private static SmtpOptions MailpitSettings() => new()
    {
        Host = Environment.GetEnvironmentVariable("TestMailpitSmtpHost") ?? "127.0.0.1",
        Port = int.Parse(Environment.GetEnvironmentVariable("TestMailpitSmtpPort") ?? "1025"),
        Security = "None", TimeoutSeconds = 2
    };

    private static async Task<string> MailpitToken(string email)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("TestMailpitHttpUrl")!), Timeout = TimeSpan.FromSeconds(5) };
        using var messages = await http.GetFromJsonAsync<JsonDocument>("/api/v1/messages");
        var entry = Assert.Single(messages!.RootElement.GetProperty("messages").EnumerateArray(), x =>
            x.GetProperty("To").EnumerateArray().Any(to => to.GetProperty("Address").GetString() == email));
        using var message = await http.GetFromJsonAsync<JsonDocument>("/api/v1/message/" + entry.GetProperty("ID").GetString());
        var text = message!.RootElement.GetProperty("Text").GetString()!;
        Assert.Contains("expires at", text);
        var token = Regex.Match(text, "Verification token: ([A-Fa-f0-9]{64})");
        Assert.True(token.Success);
        return token.Groups[1].Value;
    }
}

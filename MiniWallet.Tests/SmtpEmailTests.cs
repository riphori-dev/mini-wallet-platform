using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using MiniWallet.Users.Application;
using MiniWallet.Users.Infrastructure;
using Xunit;

namespace MiniWallet.Tests;

public sealed class SmtpEmailTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sends_verification_content_and_keeps_success_after_lost_quit(bool loseQuit)
    {
        await using var server = new SmtpTestServer(loseQuit: loseQuit);
        var expires = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var token = new string('A', 64);
        Assert.True(await Sender(server.Port).SendVerificationAsync("player@example.test", token, expires, default));
        using var message = MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(await server.Message.Task)));
        Assert.Equal("no-reply@miniwallet.test", Assert.Single(message.From.Mailboxes).Address);
        Assert.Equal("player@example.test", Assert.Single(message.To.Mailboxes).Address);
        Assert.Equal("Verify your MiniWallet email", message.Subject);
        Assert.Contains(token, message.TextBody);
        Assert.Contains("2030-01-02 03:04:05 UTC", message.TextBody);
        Assert.Contains("POST /api/v1/email-verifications", message.TextBody);
        Assert.Contains("used once", message.TextBody);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Development_preview_only_contains_SMTP_accepted_messages(bool reject)
    {
        await using var server = new SmtpTestServer(reject: reject);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Smtp", ["Email:Smtp:Host"] = "127.0.0.1",
            ["Email:Smtp:Port"] = server.Port.ToString(), ["Email:Smtp:Security"] = "None"
        }).Build();
        using var services = new ServiceCollection().AddLogging().AddVerificationEmail(configuration, true).BuildServiceProvider();
        Assert.Equal(!reject, await services.GetRequiredService<IEmailSender>()
            .SendVerificationAsync("player@example.test", new string('B', 64), DateTimeOffset.UtcNow.AddHours(24), default));
        Assert.Equal(reject ? 0 : 1, services.GetRequiredService<DevelopmentMailbox>().Read().Length);
    }

    [Fact]
    public async Task Stalled_SMTP_is_bounded_and_caller_cancellation_is_respected()
    {
        await using var server = new SmtpTestServer(stall: true);
        var attempt = Sender(server.Port).SendVerificationAsync("player@example.test", "token", DateTimeOffset.UtcNow, default);
        Assert.Same(attempt, await Task.WhenAny(attempt, Task.Delay(TimeSpan.FromSeconds(5))));
        Assert.False(await attempt);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sender(server.Port)
            .SendVerificationAsync("player@example.test", "token", DateTimeOffset.UtcNow, cancelled.Token));
    }

    [Theory]
    [InlineData("None", false)]
    [InlineData("Auto", true)]
    [InlineData("StartTls", false, "user", null)]
    public void Rejects_insecure_or_invalid_SMTP_configuration(string security, bool development,
        string? username = null, string? password = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "Smtp", ["Email:Smtp:Security"] = security,
            ["Email:Smtp:Username"] = username, ["Email:Smtp:Password"] = password
        }).Build();
        using var services = new ServiceCollection().AddLogging().AddVerificationEmail(configuration, development).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<SmtpOptions>>().Value);
    }

    private static SmtpEmailSender Sender(int port) => new(Options.Create(new SmtpOptions
    {
        Host = "127.0.0.1", Port = port, Security = "None", TimeoutSeconds = 1
    }), NullLogger<SmtpEmailSender>.Instance);

    // A minimal SMTP peer lets us force rejection, a stalled connection and a lost QUIT response.
    private sealed class SmtpTestServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task serving;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public TaskCompletionSource<string> Message { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SmtpTestServer(bool reject = false, bool stall = false, bool loseQuit = false)
        {
            listener.Start();
            serving = Serve(reject, stall, loseQuit);
        }

        private async Task Serve(bool reject, bool stall, bool loseQuit)
        {
            try
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token);
                if (stall) { await Task.Delay(Timeout.Infinite, stop.Token); return; }
                using var reader = new StreamReader(client.GetStream());
                using var writer = new StreamWriter(client.GetStream()) { AutoFlush = true, NewLine = "\r\n" };
                await writer.WriteLineAsync("220 test SMTP");
                while (await reader.ReadLineAsync(stop.Token) is { } line)
                {
                    if (line.StartsWith("EHLO") || line.StartsWith("HELO")) await writer.WriteLineAsync("250 test");
                    else if (line.StartsWith("MAIL FROM")) await writer.WriteLineAsync(reject ? "550 sender rejected" : "250 OK");
                    else if (line == "DATA")
                    {
                        await writer.WriteLineAsync("354 send data");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync(stop.Token) is { } part && part != ".") data.AppendLine(part);
                        Message.TrySetResult(data.ToString());
                        await writer.WriteLineAsync("250 accepted");
                        if (loseQuit) return;
                    }
                    else if (line == "QUIT") { await writer.WriteLineAsync("221 bye"); return; }
                    else await writer.WriteLineAsync("250 OK");
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            listener.Stop();
            await serving;
            stop.Dispose();
        }
    }
}

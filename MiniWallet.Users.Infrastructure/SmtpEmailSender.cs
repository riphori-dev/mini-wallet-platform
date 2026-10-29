using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MiniWallet.Users.Application;
using MiniWallet.Users.Infrastructure.Development;

namespace MiniWallet.Users.Infrastructure;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromAddress { get; set; } = "no-reply@miniwallet.test";
    public string FromName { get; set; } = "MiniWallet";
    public string Security { get; set; } = "StartTls";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>Sends verification messages over SMTP with a bounded total delivery time.</summary>
/// <remarks>Success means the SMTP server accepted the message, not that it reached an internet inbox.</remarks>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendVerificationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Verify your MiniWallet email";
        message.Body = new TextPart("plain")
        {
            Text = $$"""
                Hi,

                Please verify your email address to finish setting up your MiniWallet account.

                Verification token: {{token}}

                In Users Swagger, open POST /api/v1/email-verifications and submit:
                {"token":"{{token}}"}

                You can also paste the token into the API explorer's email verification form.
                This token can be used once and expires at {{expiresAt.UtcDateTime:yyyy-MM-dd HH:mm:ss}} UTC.
                If it expires or delivery fails, request a new message using your registered credentials.
                If you did not register, you can ignore this email.
                """
        };

        using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };
        try
        {
            var security = settings.Security switch
            {
                "None" => SecureSocketOptions.None,
                "SslOnConnect" => SecureSocketOptions.SslOnConnect,
                _ => SecureSocketOptions.StartTls
            };
            await client.ConnectAsync(settings.Host, settings.Port, security, timeout.Token);
            if (!string.IsNullOrEmpty(settings.Username))
                await client.AuthenticateAsync(settings.Username, settings.Password!, timeout.Token);
            await client.SendAsync(message, timeout.Token);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            // SMTP replies can contain addresses or other sensitive content. Do not log the message, token or credentials.
            logger.LogWarning("Verification SMTP delivery unavailable ({FailureType}).", exception.GetType().Name);
            return false;
        }

        // Acceptance has already happened. A failed QUIT must not misreport the accepted send as a rejection.
        try { await client.DisconnectAsync(true, timeout.Token); }
        catch (Exception) { }
        return true;
    }
}

internal sealed class DevelopmentSmtpEmailSender(SmtpEmailSender smtp, DevelopmentMailbox mailbox) : IEmailSender
{
    public async Task<bool> SendVerificationAsync(string email, string token, DateTimeOffset expiresAt, CancellationToken ct)
    {
        if (!await smtp.SendVerificationAsync(email, token, expiresAt, ct)) return false;
        await mailbox.SendVerificationAsync(email, token, expiresAt, ct);
        return true;
    }
}

public static class VerificationEmailSetup
{
    public static IServiceCollection AddVerificationEmail(this IServiceCollection services, IConfiguration configuration,
        bool development)
    {
        if (development) services.AddSingleton<DevelopmentMailbox>();
        var provider = configuration["Email:Provider"] ?? (development ? "Mailbox" : "Unavailable");
        if (provider == "Mailbox" && development)
            return services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<DevelopmentMailbox>());
        if (provider == "Unavailable") return services.AddSingleton<IEmailSender, UnavailableEmailSender>();
        if (provider != "Smtp") throw new InvalidOperationException("Unsupported Email:Provider for this environment.");

        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("Email:Smtp"))
            .Validate(s => !string.IsNullOrWhiteSpace(s.Host) && s.Port is >= 1 and <= 65535,
                "SMTP host and a valid port are required.")
            .Validate(s => MailboxAddress.TryParse(s.FromAddress, out _) && !string.IsNullOrWhiteSpace(s.FromName),
                "A valid SMTP sender address and name are required.")
            .Validate(s => s.TimeoutSeconds is >= 1 and <= 30, "SMTP timeout must be between 1 and 30 seconds.")
            .Validate(s => s.Security is "StartTls" or "SslOnConnect" || (development && s.Security == "None"),
                "SMTP requires StartTls or SslOnConnect; None is allowed only in Development.")
            .Validate(s => string.IsNullOrEmpty(s.Username) == string.IsNullOrEmpty(s.Password),
                "SMTP username and password must be configured together.")
            .ValidateOnStart();
        services.AddSingleton<SmtpEmailSender>();
        return development
            ? services.AddSingleton<IEmailSender, DevelopmentSmtpEmailSender>()
            : services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
    }
}

using FluentValidation;
using MiniWallet.Users.Domain;

namespace MiniWallet.Users.Application;

public sealed class UserUseCases(IUserStore store, IPasswordService passwords,
    IVerificationSecrets secrets, IEmailSender email, TimeProvider clock,
    IValidator<RegisterRequest> registrationValidator, IValidator<CredentialsRequest> credentialsValidator,
    IValidator<VerifyRequest> verificationValidator)
{
    private static async Task Validate<T>(IValidator<T> validator, T request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        if (!result.IsValid) throw new UseCaseFailure(400, "invalid_request", "Invalid request.",
            result.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }

    public async Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        await Validate(registrationValidator, request, ct);
        var user = User.Register(EmailAddress.Parse(request.Email), passwords.Hash(request.Password));
        var secret = secrets.Create();
        var now = clock.GetUtcNow();
        var expires = now.AddHours(24);
        user.SetVerification(secret.Hash, expires, now);
        if (!await store.RegisterAsync(user, now, ct))
            throw new UseCaseFailure(409, "email_already_registered", "This email is already registered. Sign in or resend verification using your credentials.");
        // Registration is already committed. Failure here must not undo or misreport acceptance.
        var sent = await Send(user.Email, secret.Token, expires);
        return new(user.Id, user.Email, false, "pending", sent ? "sent" : "unavailable");
    }

    public async Task VerifyAsync(VerifyRequest request, CancellationToken ct)
    {
        await Validate(verificationValidator, request, ct);
        if (!await store.VerifyAsync(secrets.Hash(request.Token), clock.GetUtcNow(), ct))
            throw new UseCaseFailure(400, "invalid_verification_token", "The token is invalid, expired or already used.");
    }

    public async Task ResendAsync(CredentialsRequest request, CancellationToken ct)
    {
        var user = await Authenticate(request, ct);
        if (user.EmailVerified) return;
        var secret = secrets.Create();
        var now = clock.GetUtcNow();
        var expires = now.AddHours(24);
        if (!await store.ReplaceVerificationAsync(user.Id, secret.Hash, expires, now, ct)) return;
        if (!await Send(user.Email, secret.Token, expires))
            throw new UseCaseFailure(503, "verification_delivery_unavailable", "Retry verification resend with the same credentials.");
    }

    public async Task<VerifiedIdentity> LoginAsync(CredentialsRequest request, CancellationToken ct)
    {
        var user = await Authenticate(request, ct);
        if (!user.EmailVerified)
            throw new UseCaseFailure(403, "email_not_verified", "Verify your email before signing in.");
        return new(user.Id, user.Role);
    }

    private async Task<bool> Send(string address, string token, DateTimeOffset expires)
    {
        try { return await email.SendVerificationAsync(address, token, expires, CancellationToken.None); }
        catch (Exception) { return false; } // Delivery failure cannot invalidate an already committed registration.
    }

    private async Task<User> Authenticate(CredentialsRequest request, CancellationToken ct)
    {
        await Validate(credentialsValidator, request, ct);
        var user = await store.FindByEmailAsync(EmailAddress.Parse(request.Email).Value, ct);
        if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
            throw new UseCaseFailure(401, "invalid_credentials", "Invalid credentials.");
        return user;
    }
}


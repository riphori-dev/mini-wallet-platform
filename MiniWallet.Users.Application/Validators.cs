using FluentValidation;
using MiniWallet.Users.Domain;

namespace MiniWallet.Users.Application;

internal static class EmailRules
{
    public static bool Valid(string? email)
    {
        if (email is null) return false;
        try { EmailAddress.Parse(email); return true; }
        catch (ArgumentException) { return false; }
    }
}
public sealed class RegistrationValidator : AbstractValidator<RegisterRequest>
{
    public RegistrationValidator()
    {
        RuleFor(x => x.Email).Must(EmailRules.Valid).WithMessage("A valid email of at most 254 characters is required.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}
public sealed class CredentialsValidator : AbstractValidator<CredentialsRequest>
{
    public CredentialsValidator()
    {
        RuleFor(x => x.Email).Must(EmailRules.Valid);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
public sealed class VerificationValidator : AbstractValidator<VerifyRequest>
{
    public VerificationValidator() => RuleFor(x => x.Token).NotEmpty().Matches("^[A-Fa-f0-9]{64}$");
}
public sealed class MachineTokenValidator : AbstractValidator<MachineTokenRequest>
{
    public MachineTokenValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ClientSecret).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Scope).NotEmpty().Must(x => x is "transactions:write" or "players:provision");
    }
}


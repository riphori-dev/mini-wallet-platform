using FluentValidation;

namespace MiniWallet.Application.Services;

/// <summary>An internal registration event delivered at least once by Users.</summary>
/// <param name="EventId">Stable, nonempty delivery event GUID used for inbox deduplication.</param>
/// <param name="PlayerId">Nonempty registered player GUID supplied by the authenticated internal sender.</param>
/// <param name="OccurredAt">Registration timestamp later than DateTimeOffset.MinValue.</param>
/// <param name="Version">Event schema version; only 1 is accepted.</param>
public sealed record PlayerRegistered(Guid EventId, Guid PlayerId, DateTimeOffset OccurredAt, int Version = 1);
public enum ProvisioningOutcome { Accepted, EventConflict }
public interface IPlayerProvisioner
{
    Task<ProvisioningOutcome> ProvisionAsync(PlayerRegistered registration, CancellationToken ct);
}
public sealed class ProvisioningFailure(int status, string code, string message,
    Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public Dictionary<string, string[]>? Errors { get; } = errors;
}
public sealed class PlayerRegisteredValidator : AbstractValidator<PlayerRegistered>
{
    public PlayerRegisteredValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.PlayerId).NotEmpty();
        RuleFor(x => x.Version).Equal(1);
        RuleFor(x => x.OccurredAt).GreaterThan(DateTimeOffset.MinValue);
    }
}
public sealed class ProvisionPlayer(IPlayerProvisioner provisioner, IValidator<PlayerRegistered> validator)
{
    public async Task ExecuteAsync(PlayerRegistered registration, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(registration, ct);
        if (!validation.IsValid) throw new ProvisioningFailure(400, "invalid_request", "Invalid registration event.",
            validation.Errors.GroupBy(x => x.PropertyName).ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray()));
        if (await provisioner.ProvisionAsync(registration, ct) == ProvisioningOutcome.EventConflict)
            throw new ProvisioningFailure(409, "event_conflict", "Event ID already belongs to a different registration.");
    }
}


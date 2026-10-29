namespace MiniWallet.Application.Interfaces;

/// <summary>Best-effort notification that a player's committed balance changed.</summary>
public interface IBalanceNotifications
{
    Task ChangedAsync(Guid playerId, string currency, CancellationToken ct);
}

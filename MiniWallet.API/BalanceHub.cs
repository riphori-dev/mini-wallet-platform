using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MiniWallet.API.Wallet;
using MiniWallet.Application.Interfaces;

namespace MiniWallet.API;

/// <summary>Player-only balance invalidations. Clients refresh balances through the REST API.</summary>
[Authorize(Policy = WalletAuthorization.PlayerRead)]
public sealed class BalanceHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(Guid.Parse(Context.User!.FindFirst("sub")!.Value)));
        await base.OnConnectedAsync();
    }

    internal static string Group(Guid playerId) => "player:" + playerId.ToString("D");
}

public sealed class BalanceNotifications(IHubContext<BalanceHub> hub) : IBalanceNotifications
{
    public Task ChangedAsync(Guid playerId, string currency, CancellationToken ct) =>
        hub.Clients.Group(BalanceHub.Group(playerId)).SendAsync("BalanceChanged", new { currency }, ct);
}

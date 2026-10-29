extern alias wallet;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MiniWallet.Application.Interfaces;
using Xunit;

namespace MiniWallet.Tests.Balance;

public class BalanceHubTests
{
    private static WebApplicationFactory<wallet::Program> Host() => new WebApplicationFactory<wallet::Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = "Host=localhost;Database=unused;Username=unused;Password=unused" }));
        });

    private static string Token(Guid player, string role = "player", string verified = "true") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("miniwallet-local", "wallet-api",
            [new Claim("sub", player.ToString()), new Claim("role", role), new Claim("kind", "user"),
             new Claim("scope", "wallet:read"), new Claim("email_verified", verified)],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("LOCAL-ONLY-miniwallet-signing-key-DO-NOT-DEPLOY-2026")),
                SecurityAlgorithms.HmacSha256)));

    [Fact]
    public async Task Hub_requires_verified_player_and_query_token_is_limited_to_hub()
    {
        await using var host = Host();
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/hubs/balance/negotiate?negotiateVersion=1", null)).StatusCode);
        foreach (var token in new[] { Token(Guid.NewGuid(), "admin"), Token(Guid.NewGuid(), verified: "false") })
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/hubs/balance/negotiate?negotiateVersion=1", null)).StatusCode);
        }
        client.DefaultRequestHeaders.Authorization = null;
        var player = Token(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/hubs/balance/negotiate?negotiateVersion=1&access_token=" + player, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/wallets?access_token=" + player)).StatusCode);
    }

    private static async Task<WebSocket> Connect(WebApplicationFactory<wallet::Program> host, Guid player)
    {
        var socket = await host.Server.CreateWebSocketClient().ConnectAsync(
            new Uri("ws://localhost/hubs/balance?access_token=" + Token(player)), CancellationToken.None);
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e")),
            WebSocketMessageType.Text, true, CancellationToken.None);
        Assert.Contains("{}", await Read(socket));
        // An invocation response provides a barrier after OnConnectedAsync has joined the player group.
        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"type\":1,\"invocationId\":\"ready\",\"target\":\"NoSuchMethod\",\"arguments\":[]}\u001e")),
            WebSocketMessageType.Text, true, CancellationToken.None);
        Assert.Contains("ready", await Read(socket));
        return socket;
    }

    private static async Task<string> Read(WebSocket socket)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var bytes = new byte[4096];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), timeout.Token);
        return Encoding.UTF8.GetString(bytes, 0, result.Count);
    }

    [Fact]
    public async Task Notifications_reach_only_the_owner_and_all_owner_connections()
    {
        await using var host = Host();
        using var client = host.CreateClient();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        using var first = await Connect(host, owner);
        using var second = await Connect(host, owner);
        using var outsider = await Connect(host, other);
        try
        {
        var notifications = host.Services.GetRequiredService<IBalanceNotifications>();
        await notifications.ChangedAsync(owner, "USD", CancellationToken.None);
        await notifications.ChangedAsync(other, "EUR", CancellationToken.None);
        Assert.Contains("USD", await Read(first));
        Assert.Contains("USD", await Read(second));
        var outsiderMessage = await Read(outsider);
        Assert.Contains("EUR", outsiderMessage);
        Assert.DoesNotContain("USD", outsiderMessage);
        }
        finally
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            foreach (var socket in new[] { first, second, outsider })
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", timeout.Token);
        }
    }
}

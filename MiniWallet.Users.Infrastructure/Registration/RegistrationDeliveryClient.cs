using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MiniWallet.Users.Application.Contracts;
using MiniWallet.Users.Infrastructure.Users;

namespace MiniWallet.Users.Infrastructure.Registration;

public sealed class RegistrationDeliveryOptions
{
    public bool Enabled { get; set; }
    public string UsersBaseUrl { get; set; } = "";
    public string WalletBaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public int RequestTimeoutSeconds { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 5;
}
public sealed record DeliveryAttempt(bool Success, string? Error);
public sealed class RegistrationDeliveryClient(HttpClient http, IOptions<RegistrationDeliveryOptions> options)
{
    public async Task<DeliveryAttempt> DeliverAsync(PlayerRegistrationDelivery delivery, CancellationToken ct)
    {
        var settings = options.Value;
        using var tokenResponse = await http.PostAsJsonAsync(settings.UsersBaseUrl.TrimEnd('/') + "/dev/connect/token",
            new MachineTokenRequest(settings.ClientId, settings.ClientSecret, "players:provision"), ct);
        if (!tokenResponse.IsSuccessStatusCode) return new(false, $"Token issuer HTTP {(int)tokenResponse.StatusCode}");
        var token = await tokenResponse.Content.ReadFromJsonAsync<AccessToken>(ct);
        if (token is null || string.IsNullOrWhiteSpace(token.Access_token)) return new(false, "Invalid issuer response.");
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.WalletBaseUrl.TrimEnd('/') + "/internal/v1/player-registrations");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Access_token);
        request.Content = JsonContent.Create(new { delivery.EventId, delivery.PlayerId, delivery.OccurredAt, version = 1 });
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? new(true, null) : new(false, $"Wallet HTTP {(int)response.StatusCode}");
    }
}


extern alias wallet;
extern alias users;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MiniWallet.Tests;

public class FoundationTests
{
    [Fact] public Task WalletHost() => CheckHost<wallet::Program>();
    [Fact] public Task UsersHost() => CheckHost<users::Program>();

    private static async Task CheckHost<T>() where T : class
    {
        await using var factory = new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                    ["Delivery:Enabled"] = "false"
                }));
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        if (typeof(T) == typeof(wallet::Program))
        {
            using var document = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
            Assert.True(document.RootElement.GetProperty("paths").GetProperty("/internal/v1/player-registrations")
                .GetProperty("post").GetProperty("security")[0].TryGetProperty("Bearer", out _));
        }
        var missing = await client.GetAsync("/no-such-route");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        Assert.Equal("not_found", problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }
}


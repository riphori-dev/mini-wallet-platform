extern alias wallet;
extern alias users;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MiniWallet.Tests.Financial;

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
        var ui = await client.GetStringAsync("/swagger/index.html");
        Assert.Contains("swagger-ui-bundle.js", ui);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/swagger-ui-bundle.js")).StatusCode);
        var initializer = await client.GetStringAsync("/swagger/index.js");
        Assert.Contains("v1/swagger.json", initializer);
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        using var openApi = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        var root = openApi.RootElement;
        foreach (var path in root.GetProperty("paths").EnumerateObject())
            foreach (var operation in path.Value.EnumerateObject())
            {
                Assert.False(string.IsNullOrWhiteSpace(operation.Value.GetProperty("summary").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(operation.Value.GetProperty("description").GetString()));
            }
        var schemas = root.GetProperty("components").GetProperty("schemas");
        foreach (var schema in schemas.EnumerateObject().Where(s => s.Name != "ProblemDetails"))
            foreach (var property in schema.Value.GetProperty("properties").EnumerateObject())
                Assert.False(string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString()));
        foreach (var path in root.GetProperty("paths").EnumerateObject())
            foreach (var operation in path.Value.EnumerateObject())
                foreach (var response in operation.Value.GetProperty("responses").EnumerateObject())
                    if (int.TryParse(response.Name, out var status) && status >= 400)
                        Assert.True(response.Value.GetProperty("content").TryGetProperty("application/problem+json", out _));
        if (typeof(T) == typeof(wallet::Program))
        {
            Assert.Contains("original", schemas.GetProperty("TransactionResponse").GetProperty("properties")
                .GetProperty("balanceAfter").GetProperty("description").GetString());
            Assert.Contains("internal", schemas.GetProperty("HistoryItem").GetProperty("properties")
                .GetProperty("originalBetId").GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
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


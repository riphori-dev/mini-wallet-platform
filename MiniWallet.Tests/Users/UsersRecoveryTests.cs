using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MiniWallet.Tests;

public sealed partial class UsersIntegrationTests
{
    [PostgresFact]
    public async Task Registration_transient_failure_is_retryable_and_never_partially_accepted()
    {
        var email = $"retry-{Guid.NewGuid():N}@example.test";
        var request = new { email, password = UsersDatabaseFixture.Password };
        await using var setup = fixture.Db();
        var previousDeliveries = await setup.RegistrationDeliveries.CountAsync();
        await setup.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_fail_registration() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'transient registration failure' USING ERRCODE='40001'; END; $$;
            CREATE TRIGGER test_fail_registration BEFORE INSERT ON "PlayerRegistrationDeliveries"
              FOR EACH ROW EXECUTE FUNCTION test_fail_registration();
            """);
        try
        {
            var failed = await fixture.Client.PostAsJsonAsync("/api/v1/register", request);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
            Assert.Equal("application/problem+json", failed.Content.Headers.ContentType?.MediaType);
            Assert.Equal("1", Assert.Single(failed.Headers.GetValues("Retry-After")));
            using var problem = JsonDocument.Parse(await failed.Content.ReadAsStringAsync());
            Assert.Equal("transient_unavailability", problem.RootElement.GetProperty("code").GetString());
            Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
            // Inspect through a fresh context after request transaction disposal/rollback.
            await using var verify = fixture.Db();
            Assert.False(await verify.Users.AnyAsync(x => x.Email == email));
            Assert.Equal(previousDeliveries, await verify.RegistrationDeliveries.CountAsync());
            Assert.DoesNotContain(fixture.Mailbox.Read(), message => message.Email == email);
        }
        finally
        {
            await setup.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER test_fail_registration ON "PlayerRegistrationDeliveries";
                DROP FUNCTION test_fail_registration();
                """);
        }
        Assert.Equal(HttpStatusCode.Created, (await fixture.Client.PostAsJsonAsync("/api/v1/register", request)).StatusCode);
        await using var accepted = fixture.Db();
        var user = await accepted.Users.SingleAsync(x => x.Email == email);
        Assert.Equal(1, await accepted.RegistrationDeliveries.CountAsync(x => x.PlayerId == user.Id));
    }
}


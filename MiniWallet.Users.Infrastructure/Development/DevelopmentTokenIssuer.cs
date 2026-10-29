using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MiniWallet.Users.Application.Contracts;

namespace MiniWallet.Users.Infrastructure.Development;

public sealed record DevelopmentIssuerSettings(string Issuer, string SigningKey,
    string ProviderClientId, string ProviderSecret, string InternalClientId, string InternalSecret);

public sealed class DevelopmentTokenIssuer(DevelopmentIssuerSettings settings, TimeProvider clock) : IAccessTokenIssuer
{
    public AccessToken ForUser(VerifiedIdentity identity)
    {
        if (identity.Role is not ("player" or "admin")) throw new InvalidOperationException("Unknown user role.");
        return Issue("wallet-api",
        [
            new("sub", identity.Id.ToString()), new("role", identity.Role), new("kind", "user"),
            new("email_verified", "true"),
            new("scope", identity.Role == "admin" ? "wallet:read:any" : "wallet:read")
        ]);
    }

    public AccessToken ForMachine(string clientId, string secret, string scope)
    {
        if (clientId == settings.ProviderClientId && SameSecret(secret, settings.ProviderSecret))
        {
            if (scope != "transactions:write") throw new UseCaseFailure(403, "forbidden", "Client scope is not allowed.");
            return Issue("wallet-api", [new("sub", clientId), new("provider_id", clientId),
                new("role", "provider"), new("kind", "machine"), new("scope", scope)]);
        }
        if (clientId == settings.InternalClientId && SameSecret(secret, settings.InternalSecret))
        {
            if (scope != "players:provision") throw new UseCaseFailure(403, "forbidden", "Client scope is not allowed.");
            return Issue("wallet-internal", [new("sub", clientId), new("role", "internal"),
                new("kind", "machine"), new("scope", scope)]);
        }
        throw new UseCaseFailure(401, "invalid_credentials", "Invalid client credentials.");
    }

    public static bool SameSecret(string candidate, string expected) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(candidate)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

    private AccessToken Issue(string audience, Claim[] claims)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var token = new JwtSecurityToken(settings.Issuer, audience,
            claims.Append(new Claim("jti", Guid.NewGuid().ToString())), now, now.AddMinutes(10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", 600);
    }
}


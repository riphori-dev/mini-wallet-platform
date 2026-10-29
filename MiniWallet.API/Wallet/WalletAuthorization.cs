using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace MiniWallet.API.Wallet;

public static class WalletAuthorization
{
    public const string PlayerRead = "PlayerRead";
    public const string AdminRead = "AdminRead";
    public const string ProviderWrite = "ProviderWrite";
    public const string InternalProvisioning = "InternalProvisioning";

    public static IServiceCollection AddWalletAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => Configure(options, "wallet-api"))
            .AddJwtBearer("Internal", options => Configure(options, "wallet-internal"));
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PlayerRead, policy => policy.RequireAuthenticatedUser()
                .RequireClaim("kind", "user").RequireRole("player").RequireClaim("email_verified", "true")
                .RequireAssertion(ctx => Guid.TryParse(ctx.User.FindFirst("sub")?.Value, out _) && HasScope(ctx, "wallet:read")));
            options.AddPolicy(AdminRead, policy => policy.RequireAuthenticatedUser()
                .RequireClaim("kind", "user").RequireRole("admin")
                .RequireAssertion(ctx => HasScope(ctx, "wallet:read:any")));
            options.AddPolicy(ProviderWrite, policy => policy.RequireAuthenticatedUser()
                .RequireClaim("kind", "machine").RequireRole("provider").RequireClaim("provider_id")
                .RequireAssertion(ctx => HasScope(ctx, "transactions:write")));
            options.AddPolicy(InternalProvisioning, policy => policy.AddAuthenticationSchemes("Internal")
                .RequireAuthenticatedUser().RequireClaim("kind", "machine").RequireRole("internal")
                .RequireAssertion(ctx => HasScope(ctx, "players:provision")));
        });
        return services;

        void Configure(JwtBearerOptions options, string audience)
        {
            var key = configuration["Jwt:SigningKey"];
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                IssuerSigningKey = string.IsNullOrWhiteSpace(key) ? null : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "unconfigured",
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.FromSeconds(5),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
            options.Events = new()
            {
                OnMessageReceived = context =>
                {
                    // Browser WebSocket/SSE clients cannot set an Authorization header.
                    if (audience == "wallet-api" && context.Request.Path.StartsWithSegments("/hubs/balance")
                        && context.Request.Query.TryGetValue("access_token", out var token))
                        context.Token = token;
                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await Error(context.HttpContext, 401, "unauthenticated");
                },
                OnForbidden = context => Error(context.HttpContext, 403, "forbidden")
            };
        }
    }

    private static bool HasScope(AuthorizationHandlerContext context, string scope) =>
        context.User.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Contains(scope);

    private static async Task Error(HttpContext context, int status, string code)
    {
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = code,
            Type = $"urn:miniwallet:problem:{code}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}


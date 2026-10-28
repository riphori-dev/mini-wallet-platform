using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniWallet.API;

internal static class SwaggerSetup
{
    public static void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MiniWallet Wallet",
            Version = "v1",
            Description = "Use Users /dev/token for verified player/admin credentials; /dev/connect/token for distinct provider or internal credentials. Tokens expire in 10 minutes. Paste the JWT in Authorize. Provider transaction and player/admin read contracts are described below; local issuer credentials and complete retry semantics are also in README.md and docs/API_CONTRACT.md."
        });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the JWT only. Development credentials and token requests are in README.md."
        });
        options.DocumentFilter<AuthorizedOperationsFilter>();
        options.DocumentFilter<WalletContractFilter>();
    }
}

internal sealed class AuthorizedOperationsFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var description in context.ApiDescriptions.Where(d =>
            d.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any()))
        {
            if (!document.Paths.TryGetValue("/" + description.RelativePath, out var path)) continue;
            var operation = path.Operations?.FirstOrDefault(pair =>
                string.Equals(pair.Key.ToString(), description.HttpMethod, StringComparison.OrdinalIgnoreCase)).Value;
            if (operation is null) continue;
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            }];
        }
    }
}


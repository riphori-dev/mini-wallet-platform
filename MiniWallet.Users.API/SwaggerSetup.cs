using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniWallet.Users.API;

internal static class SwaggerSetup
{
    public static void Configure(SwaggerGenOptions options)
    {
        foreach (var assembly in new[] { "MiniWallet.Users.API", "MiniWallet.Users.Application", "MiniWallet.Users.Domain", "MiniWallet.Users.Infrastructure" })
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, assembly + ".xml"), includeControllerXmlComments: true);

        options.SupportNonNullableReferenceTypes();
        options.DocumentFilter<ProblemResponsesFilter>();
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MiniWallet Users",
            Version = "v1",
            Description = "Register, read /dev/mailbox with X-Dev-Mailbox-Key, verify, then POST /dev/token. Development credentials and JSON client flow are in README.md."
        });
    }
}

// Exception and validation handlers emit Problem Details, regardless of MVC's inferred media types.
internal sealed class ProblemResponsesFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var path in document.Paths.Values)
            foreach (var operation in path.Operations!.Values)
                foreach (var response in operation.Responses ?? [])
                {
                    if (!int.TryParse(response.Key, out var status) || status < 400
                        || response.Value is not OpenApiResponse concrete) continue;
                    var schema = concrete.Content?.Values.FirstOrDefault()?.Schema;
                    concrete.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new() { Schema = schema }
                    };
                }
    }
}

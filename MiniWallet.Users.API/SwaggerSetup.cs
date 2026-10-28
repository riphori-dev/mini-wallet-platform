using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniWallet.Users.API;

internal static class SwaggerSetup
{
    public static void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MiniWallet Users",
            Version = "v1",
            Description = "Register, read /dev/mailbox with X-Dev-Mailbox-Key, verify, then POST /dev/token. Development credentials and JSON client flow are in README.md."
        });
    }
}


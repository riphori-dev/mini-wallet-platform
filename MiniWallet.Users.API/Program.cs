using Microsoft.EntityFrameworkCore;
using MiniWallet.Users.Infrastructure;
using MiniWallet.Users.API;
using FluentValidation;
using MiniWallet.Users.Application;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<UsersDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)
    .ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = ApiProblems.InvalidModelState);
builder.Services.AddExceptionHandler<UseCaseExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblems.Customize);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SwaggerSetup.Configure);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IVerificationSecrets, VerificationSecrets>();
builder.Services.AddScoped<IUserStore, UserStore>();
builder.Services.AddScoped<UserUseCases>();
builder.Services.AddScoped<IValidator<RegisterRequest>, RegistrationValidator>();
builder.Services.AddScoped<IValidator<CredentialsRequest>, CredentialsValidator>();
builder.Services.AddScoped<IValidator<VerifyRequest>, VerificationValidator>();
builder.Services.AddScoped<IValidator<MachineTokenRequest>, MachineTokenValidator>();
builder.Services.AddRegistrationDelivery(builder.Configuration);
builder.Services.AddVerificationEmail(builder.Configuration, builder.Environment.IsDevelopment());
if (builder.Environment.IsDevelopment())
{
    string Required(string name) => builder.Configuration[name]
        ?? throw new InvalidOperationException($"Missing development configuration: {name}");
    builder.Services.AddSingleton(new DevelopmentIssuerSettings(Required("Jwt:Issuer"), Required("Jwt:SigningKey"),
        Required("Development:ProviderClientId"), Required("Development:ProviderSecret"),
        Required("Development:InternalClientId"), Required("Development:InternalSecret")));
    builder.Services.AddSingleton<IAccessTokenIssuer, DevelopmentTokenIssuer>();
}

var app = builder.Build();
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DatabaseSetup.MigrateAsync(scope.ServiceProvider.GetRequiredService<UsersDbContext>());
    if (app.Environment.IsDevelopment())
    {
        await DevelopmentIdentitySeeder.SeedAdminAsync(scope.ServiceProvider.GetRequiredService<UsersDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordService>(),
            app.Configuration["Development:AdminEmail"]!, app.Configuration["Development:AdminPassword"]!);
        await DevelopmentIdentitySeeder.SeedDemoAsync(scope.ServiceProvider.GetRequiredService<UsersDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordService>());
    }
    return;
}

// Hosts never migrate. Compose runs the migrator to completion before starting hosts.
app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.Run();

public partial class Program;


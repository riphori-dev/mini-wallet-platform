using Microsoft.EntityFrameworkCore;
using MiniWallet.Infrastructure;
using MiniWallet.API;
using FluentValidation;
using MiniWallet.Application;
using System.Text.Json.Serialization;
using MiniWallet.Application.Interfaces;
using MiniWallet.Application.Validations;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<WalletDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)
    .ConfigureApiBehaviorOptions(options =>
    options.InvalidModelStateResponseFactory = ApiProblems.InvalidModelState);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblems.Customize);
builder.Services.AddExceptionHandler<WalletExceptionHandler>();
builder.Services.AddScoped<IPlayerProvisioner, PlayerProvisioner>();
builder.Services.AddScoped<ProvisionPlayer>();
builder.Services.AddScoped<IValidator<PlayerRegistered>, PlayerRegisteredValidator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITransactionStore, TransactionStore>();
builder.Services.AddScoped<ITransactionHandler, BetHandler>();
builder.Services.AddScoped<ITransactionHandler, WinHandler>();
builder.Services.AddScoped<IValidator<TransactionRequest>, TransactionValidator>();
builder.Services.AddScoped<SubmitTransaction>();
builder.Services.AddScoped<IWalletReader, WalletReader>();
builder.Services.AddScoped<IValidator<HistoryRequest>, HistoryValidator>();
builder.Services.AddScoped<ReadWallets>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SwaggerSetup.Configure);
builder.Services.AddWalletAuthorization(builder.Configuration);

var app = builder.Build();
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DatabaseSetup.MigrateAsync(scope.ServiceProvider.GetRequiredService<WalletDbContext>());
    if (app.Environment.IsDevelopment())
        await DevelopmentWalletFixture.SeedAsync(scope.ServiceProvider.GetRequiredService<WalletDbContext>());
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;


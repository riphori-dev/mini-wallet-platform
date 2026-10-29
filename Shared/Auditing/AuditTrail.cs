using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace MiniWallet.Auditing;

public sealed record AuditRecord(Guid Id, DateTimeOffset Timestamp, string Service, string Instance,
    string TraceId, string Method, string Route, string? ActorId, string? Role,
    int Status, string Outcome, long DurationMs, IReadOnlyDictionary<string, string> Details);

public interface IAuditWriter
{
    Task WriteAsync(AuditRecord record, CancellationToken ct);
}

public sealed class PostgresAuditWriter(IConfiguration configuration) : IAuditWriter
{
    public async Task WriteAsync(AuditRecord record, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(configuration.GetConnectionString("Database"));
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO "AuditEvents" ("Id", "CreatedAt", "Record") VALUES (@id, @at, @record)
            """, connection);
        command.Parameters.AddWithValue("id", record.Id);
        command.Parameters.AddWithValue("at", record.Timestamp);
        command.Parameters.AddWithValue("record", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(record));
        await command.ExecuteNonQueryAsync(ct);
    }
}

public static class AuditDetails
{
    private const string Key = "MiniWallet.AuditDetails";
    // Only explicit metadata, never automatic request/response or header capture.
    public static void Set(HttpContext context, string name, string? value)
    {
        if (value is null) return;
        if (context.Items[Key] is not Dictionary<string, string> details)
            context.Items[Key] = details = new();
        details[name] = value.Length > 128 ? value[..128] : value;
    }

    internal static IReadOnlyDictionary<string, string> Get(HttpContext context) =>
        context.Items[Key] as Dictionary<string, string> ?? new();
}

public sealed class AuditMiddleware(RequestDelegate next, string service)
{
    public async Task InvokeAsync(HttpContext context, IAuditWriter writer, ILogger<AuditMiddleware> logger)
    {
        // Hub lifetimes, health probes, static assets and Swagger are not audit actions.
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api") && !path.StartsWithSegments("/internal")
            && !path.StartsWithSegments("/dev")) { await next(context); return; }
        var started = Stopwatch.GetTimestamp();
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = traceId, ["Service"] = service, ["Instance"] = Environment.MachineName
        });
        var unhandled = false;
        try { await next(context); }
        catch { unhandled = true; throw; }
        finally
        {
            var status = unhandled ? 500 : context.Response.StatusCode;
            if (Guid.TryParse(context.Request.RouteValues["playerId"]?.ToString(), out var target))
                AuditDetails.Set(context, "TargetPlayerId", target.ToString());
            var details = AuditDetails.Get(context);
            var authenticated = context.User.Identity?.IsAuthenticated == true;
            var record = new AuditRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, service, Environment.MachineName,
                traceId, context.Request.Method,
                (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched",
                authenticated ? context.User.FindFirst("sub")?.Value : details.GetValueOrDefault("ActorId"),
                authenticated ? context.User.FindFirst("role")?.Value : details.GetValueOrDefault("Role"),
                status, status < 400 ? "succeeded" : status < 500 ? "rejected" : "failed",
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, details);
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await writer.WriteAsync(record, timeout.Token).WaitAsync(timeout.Token);
            }
            catch (Exception)
            {
                // Best effort request trail: never turn committed work into an HTTP error.
                // Include the safe record for recovery from the retained container logs.
                logger.LogError("Audit persistence failed. AuditFallback {AuditRecord}", JsonSerializer.Serialize(record));
            }
        }
    }
}

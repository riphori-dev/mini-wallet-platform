using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.Users.Application;
using Npgsql;

namespace MiniWallet.Users.API;

internal sealed class UseCaseExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        // EF may wrap a transient database error in InvalidOperationException as well as DbUpdateException.
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
            if (cause is NpgsqlException) { exception = cause; break; }
        var failure = exception as UseCaseFailure;
        var transient = exception is NpgsqlException { IsTransient: true }
            || exception is PostgresException { SqlState: "55P03" or "57014" or "40P01" or "40001" }
            || exception is TimeoutException;
        if (failure is null && !transient) return false;
        var problem = new ProblemDetails
        {
            Status = failure?.Status ?? 503,
            Title = failure?.Message ?? "Database temporarily unavailable."
        };
        problem.Extensions["code"] = failure?.Code ?? "transient_unavailability";
        MiniWallet.Auditing.AuditDetails.Set(context, "ErrorCode", failure?.Code ?? "transient_unavailability");
        if (failure?.Errors is not null) problem.Extensions["errors"] = failure.Errors;
        ApiProblems.Customize(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        context.Response.StatusCode = problem.Status.Value;
        if (problem.Status == 503) context.Response.Headers.RetryAfter = "1";
        if (problem.Status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}


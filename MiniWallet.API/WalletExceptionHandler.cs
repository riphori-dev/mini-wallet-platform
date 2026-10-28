using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MiniWallet.Application;
using Npgsql;

namespace MiniWallet.API;

internal sealed class WalletExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        // EF's execution strategy may wrap transient PostgreSQL failures in InvalidOperationException.
        for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
            if (cause is NpgsqlException) { exception = cause; break; }
        var failure = exception as ProvisioningFailure;
        var walletFailure = exception as WalletFailure;
        if (failure is null && walletFailure is null && exception is not NpgsqlException { IsTransient: true }
            && exception is not PostgresException { SqlState: "55P03" or "57014" or "40P01" or "40001" }
            && exception is not TimeoutException) return false;
        var problem = new ProblemDetails
        {
            Status = walletFailure?.Status ?? failure?.Status ?? 503,
            Title = walletFailure?.Message ?? failure?.Message ?? "Database temporarily unavailable."
        };
        problem.Extensions["code"] = walletFailure?.Code ?? failure?.Code ?? "transient_unavailability";
        if (failure?.Errors is not null) problem.Extensions["errors"] = failure.Errors;
        if (walletFailure?.Errors is not null) problem.Extensions["errors"] = walletFailure.Errors;
        ApiProblems.Customize(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        context.Response.StatusCode = problem.Status.Value;
        if (problem.Status == 503) context.Response.Headers.RetryAfter = "1";
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}


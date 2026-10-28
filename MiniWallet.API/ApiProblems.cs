using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MiniWallet.API;

internal static class ApiProblems
{
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Extensions.TryAdd("code", problem.Status switch
        {
            400 => "invalid_request",
            401 => "unauthenticated",
            403 => "forbidden",
            404 => "not_found",
            503 => "transient_unavailability",
            _ => "internal_error"
        });
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        problem.Type = $"urn:miniwallet:problem:{problem.Extensions["code"]}";
        if (problem.Status >= 500) problem.Detail = "The service could not complete the request.";
    }

    public static IActionResult InvalidModelState(ActionContext context)
    {
        var problem = new ValidationProblemDetails(context.ModelState) { Status = 400, Title = "Invalid request" };
        Customize(new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem });
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }
}


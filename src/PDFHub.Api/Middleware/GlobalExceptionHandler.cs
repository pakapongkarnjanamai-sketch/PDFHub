using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PDFHub.Application.Exceptions;

namespace PDFHub.Api.Middleware;

/// <summary>Translates exceptions to RFC 7807 ProblemDetails.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException
            && (httpContext.RequestAborted.IsCancellationRequested || cancellationToken.IsCancellationRequested))
        {
            return true; // client went away
        }

        ProblemDetails problem = exception switch
        {
            ValidationException v => new ValidationProblemDetails(v.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = 400,
                Title = "One or more validation errors occurred.",
            },
            BusinessRuleException => new() { Status = 400, Title = "The request violates a business rule.", Detail = exception.Message },
            KeyNotFoundException => new() { Status = 404, Title = "Resource not found", Detail = exception.Message },
            // A file-system permission problem (e.g. the app pool cannot write the data folder), not a login problem.
            UnauthorizedAccessException => new() { Status = 403, Title = "Forbidden" },
            ArgumentException => new() { Status = 400, Title = "Invalid request" },
            OperationCanceledException => new() { Status = 408, Title = "Request cancelled" },
            InvalidOperationException => new() { Status = 409, Title = "Operation not allowed in current state" },
            _ => new() { Status = 500, Title = "An unexpected error occurred" },
        };

        var method = Sanitize(httpContext.Request.Method);
        var path = Sanitize(httpContext.Request.Path.Value);
        if (problem.Status >= 500)
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", method, path);
        else
            logger.LogInformation("{Status} for {Method} {Path}: {Message}", problem.Status, method, path, Sanitize(exception.Message));

        problem.Type = $"https://httpstatuses.io/{problem.Status}";
        problem.Instance = httpContext.Request.Path;
        if (environment.IsDevelopment() && problem.Status >= 500)
            problem.Detail = exception.ToString();

        httpContext.Response.StatusCode = problem.Status!.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    // Prevent log forging (CWE-117).
    private static string Sanitize(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\r', '_').Replace('\n', '_');
}

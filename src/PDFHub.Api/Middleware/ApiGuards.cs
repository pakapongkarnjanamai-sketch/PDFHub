using Microsoft.AspNetCore.Mvc;
using PDFHub.Api.Extensions;

namespace PDFHub.Api.Middleware;

public static class ApiGuards
{
    /// <summary>Header the SPA sends on every write. A cross-site HTML form cannot set custom headers.</summary>
    public const string RequestedWithHeader = "X-Requested-With";
    public const string RequestedWithValue = "PDFHub";

    public static IApplicationBuilder UseApiGuards(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api"))
        {
            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method)
                && request.Headers[RequestedWithHeader] != RequestedWithValue)
            {
                await WriteProblemAsync(context, 400, "Missing X-Requested-With header", null);
                return;
            }

            // Accounts created with a temporary password must pick their own before doing anything else.
            if (context.User.MustChangePassword() && !request.Path.StartsWithSegments("/api/session"))
            {
                await WriteProblemAsync(context, 403, "Password change required", "password-change-required");
                return;
            }
        }
        await next();
    });

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string? type)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title, Type = type ?? $"https://httpstatuses.io/{status}" },
            (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}

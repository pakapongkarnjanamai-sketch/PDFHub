using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using PDFHub.Domain.Entities;
using PDFHub.Application;

namespace PDFHub.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            // Thai text as-is rather than \u escapes.
            o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        });
        services.AddProblemDetails();

        // Room for one PDF of the configured maximum size plus the rest of the form. IIS in-process hosting
        // has its own 30 MB default on top of this, and IIS request filtering reads web.config.
        var maxRequestBytes = ((configuration.GetValue<long?>("PdfHub:MaxPdfSizeMB") ?? 100) + 10) * 1024 * 1024;
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = maxRequestBytes);
        services.Configure<IISServerOptions>(o => o.MaxRequestBodySize = maxRequestBytes);
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = maxRequestBytes);

        services.AddApiAuthentication();
        services.AddApiAuthorization();
        return services;
    }

    private static void AddApiAuthentication(this IServiceCollection services) =>
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "PDFHub.Auth";
                o.Cookie.HttpOnly = true;
                // Lax keeps the cookie off cross-site POSTs; the X-Requested-With guard covers older browsers.
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.ExpireTimeSpan = TimeSpan.FromHours(12);
                o.SlidingExpiration = true;
                // An API answers with a status code, not a redirect to a login page.
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
                o.Events.OnValidatePrincipal = SessionCookie.ValidateAsync;
            });

    private static void AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(o =>
        {
            o.AddPolicy(Policies.CanEdit, p => p.RequireRole(Roles.Admin, Roles.Editor));
            o.AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Admin));
        });
        services.AddOptions<AuthorizationOptions>().Configure<IOptions<PdfHubOptions>>((o, hub) =>
        {
            if (hub.Value.RequireLoginToView)
                o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });
    }
}

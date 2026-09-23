using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;
using PDFHub;
using PDFHub.Data;
using PDFHub.Services;

// Always Gregorian dates and "." decimals, even on a server set to Thai regional settings (Buddhist years).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-GB");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : null,
});
builder.Host.UseWindowsService(o => o.ServiceName = "PDFHub");

builder.Services.Configure<PdfHubOptions>(builder.Configuration.GetSection(PdfHubOptions.SectionName));
builder.Services.AddSingleton<AppPaths>();
builder.Services.AddDbContext<AppDbContext>((sp, o) => o.UseSqlite(sp.GetRequiredService<AppPaths>().ConnectionString));
builder.Services.AddSingleton<PdfStorage>();
builder.Services.AddScoped<PdfCodeValidator>();
builder.Services.AddScoped<DrawingImporter>();
builder.Services.AddHostedService<DatabaseBackupService>();

// Room for one PDF of the configured maximum size plus the rest of the form.
var maxRequestBytes = ((builder.Configuration.GetValue<long?>("PdfHub:MaxPdfSizeMB") ?? 100) + 10) * 1024 * 1024;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(f => f.MultipartBodyLengthLimit = maxRequestBytes);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/account/login";
        o.LogoutPath = "/account/logout";
        o.AccessDeniedPath = "/account/access-denied";
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
        o.Cookie.Name = "PDFHub.Auth";
        o.Events.OnValidatePrincipal = UserSession.ValidateAsync;
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.CanEdit, p => p.RequireRole(Roles.Admin, Roles.Editor));
    o.AddPolicy(Policies.Admin, p => p.RequireRole(Roles.Admin));
});
builder.Services.AddOptions<AuthorizationOptions>().Configure<IOptions<PdfHubOptions>>((o, hub) =>
{
    if (hub.Value.RequireLoginToView)
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// Send Thai text as-is instead of &#xE23; entities (the default only allows Basic Latin).
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Admin", Policies.Admin);
    o.Conventions.AuthorizeFolder("/Import", Policies.CanEdit);
    o.Conventions.AllowAnonymousToPage("/Account/Login");
    o.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    o.Conventions.AllowAnonymousToPage("/Error");
});

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/error", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Accounts created with a temporary password must pick their own before doing anything else.
app.Use(async (context, next) =>
{
    if (context.User.HasClaim(UserSession.MustChangePasswordClaim, "1") &&
        !context.Request.Path.StartsWithSegments("/account"))
    {
        context.Response.Redirect("/account/password");
        return;
    }
    await next();
});

app.MapRazorPages();
app.MapPdfHubEndpoints();

app.Run();

public partial class Program { }

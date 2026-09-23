using System.Globalization;
using Microsoft.AspNetCore.StaticFiles;
using PDFHub.Api.Extensions;
using PDFHub.Api.Middleware;
using PDFHub.Application;
using PDFHub.Infrastructure;
using PDFHub.Infrastructure.Persistence;

// Always Gregorian dates and "." decimals, even on a server set to Thai regional settings (Buddhist years).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-GB");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

app.UseExceptionHandler();
app.UseStatusCodePages();

// The React build lives in wwwroot. index.html must never be cached, or a deploy would keep serving the old bundle.
var spaFiles = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name == "index.html")
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    },
};
app.UseStaticFiles(spaFiles);

app.UseAuthentication();
app.UseAuthorization();
app.UseApiGuards();

app.MapControllers();
// Unknown API routes are a 404, not the React page.
app.Map("/api/{**path}", () => Results.NotFound());
// Deep links such as /drawings/NB-06618 are React routes.
// Anonymous even with RequireLoginToView: the page has to load to show the login form.
app.MapFallbackToFile("index.html", spaFiles).AllowAnonymous();

app.Run();

public partial class Program { }

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PDFHub.Application;
using PDFHub.Application.Abstractions;
using PDFHub.Infrastructure.Persistence;
using PDFHub.Infrastructure.Services;
using PDFHub.Infrastructure.Workers;

namespace PDFHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PdfHubOptions>()
            .Bind(configuration.GetSection(PdfHubOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.DataRoot), "PdfHub:DataRoot is required.")
            .Validate(o => o.MaxPdfSizeMB > 0, "PdfHub:MaxPdfSizeMB must be positive.")
            .ValidateOnStart();

        services.AddSingleton<AppPaths>();
        // Resolved per scope from AppPaths, so the data folder comes from configuration at run time (and in tests).
        services.AddDbContext<AppDbContext>((sp, o) => o.UseSqlite(sp.GetRequiredService<AppPaths>().ConnectionString));

        services.AddHttpContextAccessor();
        services.AddTransient<IDateTime, DateTimeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IPdfStorage, FileSystemPdfStorage>();
        services.AddSingleton<IDrawingSpreadsheet, ClosedXmlDrawingSpreadsheet>();

        services.AddScoped<IDataBackupStore, FileSystemDataBackupStore>();
        services.AddSingleton<BackupQueue>();
        services.AddSingleton<IBackupQueue>(sp => sp.GetRequiredService<BackupQueue>());

        services.AddHostedService<DatabaseBackupWorker>();
        services.AddHostedService<BackupWorker>();
        return services;
    }
}

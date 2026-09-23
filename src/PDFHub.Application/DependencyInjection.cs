using Microsoft.Extensions.DependencyInjection;
using PDFHub.Application.Services;

namespace PDFHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDrawingService, DrawingService>();
        services.AddScoped<ISectionService, SectionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IBackupService, BackupService>();
        return services;
    }
}

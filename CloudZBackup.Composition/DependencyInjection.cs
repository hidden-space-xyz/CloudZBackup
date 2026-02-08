namespace CloudZBackup.Composition;

using CloudZBackup.Application.Orchestrators;
using CloudZBackup.Application.Orchestrators.Interfaces;
using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods to register application and infrastructure services
/// into the dependency-injection container, keeping the composition root isolated
/// from individual layer concerns.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all application-layer services and orchestrators.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ISnapshotService, SnapshotService>();
        services.AddSingleton<IPlanService, PlanService>();
        services.AddSingleton<IOverwriteDetectionService, OverwriteDetectionService>();
        services.AddSingleton<IBackupExecutionService, BackupExecutionService>();
        services.AddSingleton<IBackupOrchestrator, BackupOrchestrator>();

        return services;
    }

    /// <summary>
    /// Registers all infrastructure-layer services (file system, hashing, etc.).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IHashingService, HashingService>();

        return services;
    }
}

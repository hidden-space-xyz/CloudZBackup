using CloudZBackup.Application.Orchestrators;
using CloudZBackup.Application.Orchestrators.Interfaces;
using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CloudZBackup.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IHashingService, HashingService>();
        services.AddSingleton<IEndpointService, EndpointService>();
        services.AddSingleton<ISnapshotService, SnapshotService>();
        services.AddSingleton<IPlanService, PlanService>();
        services.AddSingleton<IOverwriteDetectionService, OverwriteDetectionService>();
        services.AddSingleton<IExecutionService, ExecutionService>();
        services.AddSingleton<IBackupOrchestrator, BackupOrchestrator>();

        return services;
    }
}

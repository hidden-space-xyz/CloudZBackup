using CloudZBackup.Application.Abstractions;
using CloudZBackup.Application.UseCases;
using CloudZBackup.Infrastructure.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace CloudZBackup.Composition;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<IHashCalculator, Sha256HashCalculator>();
        services.AddSingleton<IExecuteBackupUseCase, ExecuteBackupUseCase>();
        return services;
    }
}

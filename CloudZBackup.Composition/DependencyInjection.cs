using CloudZBackup.Application.Abstractions.FileSystem;
using CloudZBackup.Application.Abstractions.Hashing;
using CloudZBackup.Application.Abstractions.UseCases;
using CloudZBackup.Application.UseCases;
using CloudZBackup.Infrastructure.FileSystem;
using CloudZBackup.Infrastructure.Hashing;
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

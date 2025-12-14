using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CloudZBackup.Application.Services;

public sealed class EndpointService(IFileSystemService fileSystem, ILogger<EndpointService> logger)
    : IEndpointService
{
    public void EnsureSourceExists(string sourceRoot)
    {
        if (!fileSystem.DirectoryExists(sourceRoot))
            throw new DirectoryNotFoundException($"Source directory not found: '{sourceRoot}'.");
    }

    public bool PrepareDestination(BackupMode mode, string destRoot)
    {
        bool destExists = fileSystem.DirectoryExists(destRoot);

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            if (!destExists)
            {
                fileSystem.CreateDirectory(destRoot);
                return true;
            }

            return false;
        }

        // Remove-only mode requires destination to exist
        if (!destExists)
        {
            logger.LogInformation("Destination directory does not exist. Nothing to remove.");
            throw new DirectoryNotFoundException();
        }

        return false;
    }
}

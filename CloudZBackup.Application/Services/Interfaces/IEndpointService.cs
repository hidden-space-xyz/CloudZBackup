using CloudZBackup.Domain.Enums;

namespace CloudZBackup.Application.Services.Interfaces;

public interface IEndpointService
{
    void EnsureSourceExists(string sourceRoot);
    bool PrepareDestination(BackupMode mode, string destRoot);
}

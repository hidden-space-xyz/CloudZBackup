using CloudZBackup.Application.ValueObjects;

namespace CloudZBackup.Application.Orchestrators.Interfaces;

public interface IBackupOrchestrator
{
    Task<BackupResult> ExecuteAsync(
        BackupRequest request,
        IProgress<BackupProgress>? progress,
        CancellationToken cancellationToken
    );
}

using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services.Interfaces;

public interface IExecutionService
{
    Task<BackupExecutionStats> ExecuteAsync(
        BackupMode mode,
        Plan plan,
        Snapshot sourceSnapshot,
        string sourceRoot,
        string destRoot,
        IReadOnlyCollection<RelativePath> filesToOverwrite,
        CancellationToken ct
    );
}

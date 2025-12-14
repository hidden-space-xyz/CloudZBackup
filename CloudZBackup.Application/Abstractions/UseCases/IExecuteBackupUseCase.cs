using CloudZBackup.Application.UseCases.Request;
using CloudZBackup.Application.UseCases.Result;

namespace CloudZBackup.Application.Abstractions.UseCases;

public interface IExecuteBackupUseCase
{
    Task<BackupResult> ExecuteAsync(BackupRequest request, CancellationToken cancellationToken);
}

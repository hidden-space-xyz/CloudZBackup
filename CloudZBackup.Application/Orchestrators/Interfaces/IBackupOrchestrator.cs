using CloudZBackup.Application.ValueObjects;

namespace CloudZBackup.Application.Orchestrators.Interfaces;

/// <summary>
/// The primary application use case that orchestrates a complete backup operation
/// from validation through execution.
/// </summary>
public interface IBackupOrchestrator
{
    /// <summary>
    /// Validates inputs, captures snapshots, builds a plan, and executes the backup.
    /// </summary>
    /// <param name="request">The backup request containing source, destination, and mode.</param>
    /// <param name="progress">An optional progress reporter for UI feedback.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="BackupResult"/> summarizing the completed operation.</returns>
    Task<BackupResult> ExecuteAsync(
        BackupRequest request,
        IProgress<BackupProgress>? progress,
        CancellationToken cancellationToken
    );
}

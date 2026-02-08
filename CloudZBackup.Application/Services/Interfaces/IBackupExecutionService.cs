namespace CloudZBackup.Application.Services.Interfaces;

using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Executes the low-level file-system operations (copy, delete, overwrite) described by a <see cref="Plan"/>.
/// </summary>
public interface IBackupExecutionService
{
    /// <summary>
    /// Executes the backup plan by creating directories, copying files, overwriting changed files,
    /// and deleting extra files and directories according to the specified <paramref name="mode"/>.
    /// </summary>
    /// <param name="mode">The backup mode that determines which operations are performed.</param>
    /// <param name="plan">The pre-computed backup plan.</param>
    /// <param name="sourceSnapshot">The snapshot of the source directory tree.</param>
    /// <param name="sourceRoot">The absolute path to the source root directory.</param>
    /// <param name="destRoot">The absolute path to the destination root directory.</param>
    /// <param name="filesToOverwrite">The collection of files that need to be overwritten.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="BackupResult"/> summarizing the operations performed.</returns>
    Task<BackupResult> ExecuteAsync(
        BackupMode mode,
        Plan plan,
        Snapshot sourceSnapshot,
        string sourceRoot,
        string destRoot,
        IReadOnlyCollection<RelativePath> filesToOverwrite,
        IProgress<BackupProgress>? progress,
        CancellationToken ct);
}

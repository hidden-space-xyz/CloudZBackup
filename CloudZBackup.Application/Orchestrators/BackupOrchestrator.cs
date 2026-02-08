using CloudZBackup.Application.Orchestrators.Interfaces;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CloudZBackup.Application.Orchestrators;

/// <summary>
/// Orchestrates a complete backup operation: validates inputs, captures snapshots,
/// builds a plan, detects overwrites, and delegates execution to the
/// <see cref="IBackupExecutionService"/>.
/// </summary>
public sealed class BackupOrchestrator(
    ISnapshotService snapshotService,
    IPlanService planService,
    IOverwriteDetectionService overwriteDetectionService,
    IBackupExecutionService executionService,
    IFileSystemService fileSystemService,
    ILogger<BackupOrchestrator> logger
) : IBackupOrchestrator
{
    /// <inheritdoc />
    public async Task<BackupResult> ExecuteAsync(
        BackupRequest request,
        IProgress<BackupProgress>? progress,
        CancellationToken cancellationToken
    )
    {
        (string sourceRoot, string destRoot) = fileSystemService.ValidateAndNormalize(request);

        fileSystemService.ValidateNoOverlap(sourceRoot, destRoot);

        fileSystemService.EnsureSourceExists(sourceRoot);

        bool destWasCreated = fileSystemService.PrepareDestination(request.Mode, destRoot);

        logger.LogInformation("Capturing snapshots...");

        bool needSourceMeta = request.Mode is BackupMode.Sync or BackupMode.Add;
        bool needDestMeta = request.Mode is BackupMode.Sync;

        Snapshot sourceSnapshot = snapshotService.CaptureSnapshot(
            sourceRoot,
            needSourceMeta,
            cancellationToken
        );
        Snapshot destSnapshot = destWasCreated
            ? snapshotService.CreateEmptySnapshot()
            : snapshotService.CaptureSnapshot(destRoot, needDestMeta, cancellationToken);

        logger.LogInformation("Planning operations for mode: {Mode}", request.Mode);

        Plan plan = planService.BuildPlan(request.Mode, sourceSnapshot, destSnapshot);

        List<RelativePath> filesToOverwrite = [];

        if (request.Mode == BackupMode.Sync && plan.CommonFiles.Count > 0)
        {
            logger.LogInformation(
                "Verifying {Count} existing file(s) using SHA-256...",
                plan.CommonFiles.Count
            );

            filesToOverwrite = await overwriteDetectionService.ComputeFilesToOverwriteAsync(
                commonFiles: plan.CommonFiles,
                sourceFiles: sourceSnapshot.Files,
                destFiles: destSnapshot.Files,
                sourceRoot: sourceRoot,
                destRoot: destRoot,
                ct: cancellationToken
            );
        }

        return await executionService.ExecuteAsync(
            mode: request.Mode,
            plan: plan,
            sourceSnapshot: sourceSnapshot,
            sourceRoot: sourceRoot,
            destRoot: destRoot,
            filesToOverwrite: filesToOverwrite,
            progress: progress,
            ct: cancellationToken
        );
    }
}

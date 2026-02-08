namespace CloudZBackup.Application.Services;

using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Options;

/// <summary>
/// Executes the file-system operations described by a <see cref="Plan"/>,
/// including directory creation, file copying, overwriting, and deletion.
/// Concurrency is controlled by <see cref="BackupOptions"/>.
/// </summary>
public sealed class BackupExecutionService(
    IFileSystemService fileSystem,
    IOptions<BackupOptions> options) : IBackupExecutionService
{
    /// <inheritdoc />
    public async Task<BackupResult> ExecuteAsync(
        BackupMode mode,
        Plan plan,
        Snapshot sourceSnapshot,
        string sourceRoot,
        string destRoot,
        IReadOnlyCollection<RelativePath> filesToOverwrite,
        IProgress<BackupProgress>? progress,
        CancellationToken ct)
    {
        int maxIoConcurrency = this.GetRecommendedIoConcurrency(destRoot);

        var ioOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxIoConcurrency,
            CancellationToken = ct,
        };

        int totalItems = ComputeTotalItems(mode, plan, filesToOverwrite);
        int processed = 0;

        int directoriesCreated = 0,
            filesCopied = 0,
            filesOverwritten = 0,
            filesDeleted = 0,
            directoriesDeleted = 0;

        void ReportProgress(string phase)
        {
            int current = Interlocked.Increment(ref processed);
            progress?.Report(new BackupProgress(phase, current, totalItems));
        }

        progress?.Report(new BackupProgress("Preparing", 0, totalItems));

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            await this.CreateDirectoriesAsync(
                plan.DirectoriesToCreate,
                destRoot,
                ioOptions,
                () =>
                {
                    Interlocked.Increment(ref directoriesCreated);
                    ReportProgress("Creating directories");
                });

            await this.CopyMissingFilesAsync(
                plan.MissingFiles,
                sourceSnapshot.Files,
                sourceRoot,
                destRoot,
                ioOptions,
                () =>
                {
                    Interlocked.Increment(ref filesCopied);
                    ReportProgress("Copying files");
                });

            if (mode == BackupMode.Sync && filesToOverwrite.Count > 0)
            {
                await this.OverwriteFilesAsync(
                    filesToOverwrite,
                    sourceSnapshot.Files,
                    sourceRoot,
                    destRoot,
                    ioOptions,
                    () =>
                    {
                        Interlocked.Increment(ref filesOverwritten);
                        ReportProgress("Overwriting files");
                    });
            }
        }

        if (mode is BackupMode.Sync or BackupMode.Remove)
        {
            await this.DeleteExtraFilesAsync(
                plan.ExtraFiles,
                destRoot,
                ioOptions,
                () =>
                {
                    Interlocked.Increment(ref filesDeleted);
                    ReportProgress("Deleting files");
                });

            foreach (RelativePath relDir in plan.TopLevelExtraDirectories)
            {
                ct.ThrowIfCancellationRequested();

                string full = fileSystem.Combine(destRoot, relDir);
                fileSystem.DeleteDirectoryIfExists(full, recursive: true);
                directoriesDeleted++;
                ReportProgress("Deleting directories");
            }
        }

        return new BackupResult(
            directoriesCreated,
            filesCopied,
            filesOverwritten,
            filesDeleted,
            directoriesDeleted);
    }

    /// <summary>
    /// Computes the total number of individual operations the plan will perform,
    /// used to report progress as a fraction of a whole.
    /// </summary>
    private static int ComputeTotalItems(
        BackupMode mode,
        Plan plan,
        IReadOnlyCollection<RelativePath> filesToOverwrite)
    {
        int total = 0;

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            total += plan.DirectoriesToCreate.Count;
            total += plan.MissingFiles.Count;

            if (mode == BackupMode.Sync)
            {
                total += filesToOverwrite.Count;
            }
        }

        if (mode is BackupMode.Sync or BackupMode.Remove)
        {
            total += plan.ExtraFiles.Count;
            total += plan.TopLevelExtraDirectories.Count;
        }

        return total;
    }

    /// <summary>
    /// Copies files that exist in the source but not in the destination.
    /// </summary>
    private async Task CopyMissingFilesAsync(
        IReadOnlyList<RelativePath> missingFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> sourceFiles,
        string sourceRoot,
        string destRoot,
        ParallelOptions options,
        Action onCopied)
    {
        if (missingFiles.Count == 0)
        {
            return;
        }

        await Parallel.ForEachAsync(
            missingFiles,
            options,
            async (relFile, ct) =>
            {
                string srcFull = fileSystem.Combine(sourceRoot, relFile);
                string dstFull = fileSystem.Combine(destRoot, relFile);

                FileEntry meta = sourceFiles[relFile];
                await fileSystem.CopyFileAsync(
                    srcFull,
                    dstFull,
                    overwrite: false,
                    meta.LastWriteTimeUtc,
                    ct);
                onCopied();
            });
    }

    /// <summary>
    /// Creates directories that exist in the source but not in the destination.
    /// </summary>
    private async Task CreateDirectoriesAsync(
        IReadOnlyList<RelativePath> dirsToCreate,
        string destRoot,
        ParallelOptions options,
        Action onCreated)
    {
        if (dirsToCreate.Count == 0)
        {
            return;
        }

        await Parallel.ForEachAsync(
            dirsToCreate,
            options,
            (relDir, ct) =>
            {
                string full = fileSystem.Combine(destRoot, relDir);
                fileSystem.CreateDirectory(full);
                onCreated();
                return ValueTask.CompletedTask;
            });
    }

    /// <summary>
    /// Deletes files from the destination that do not exist in the source.
    /// </summary>
    private async Task DeleteExtraFilesAsync(
        IReadOnlyList<RelativePath> extraFiles,
        string destRoot,
        ParallelOptions options,
        Action onDeleted)
    {
        if (extraFiles.Count == 0)
        {
            return;
        }

        await Parallel.ForEachAsync(
            extraFiles,
            options,
            (relFile, ct) =>
            {
                string dstFull = fileSystem.Combine(destRoot, relFile);
                fileSystem.DeleteFileIfExists(dstFull);
                onDeleted();
                return ValueTask.CompletedTask;
            });
    }

    /// <summary>
    /// Determines a recommended IO concurrency level based on the destination drive type.
    /// Network and removable drives are limited to sequential access.
    /// </summary>
    private int GetRecommendedIoConcurrency(string destinationRoot)
    {
        try
        {
            string? root = Path.GetPathRoot(destinationRoot);

            if (string.IsNullOrWhiteSpace(root))
            {
                return options.Value.MaxFileIoConcurrency;
            }

            DriveInfo drive = new(root);

            return drive.DriveType switch
            {
                DriveType.CDRom or DriveType.Network or DriveType.Removable => 1,
                _ => options.Value.MaxFileIoConcurrency,
            };
        }
        catch
        {
            return options.Value.MaxFileIoConcurrency;
        }
    }

    /// <summary>
    /// Overwrites destination files that have been detected as changed relative to the source.
    /// </summary>
    private async Task OverwriteFilesAsync(
        IReadOnlyCollection<RelativePath> overwriteFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> sourceFiles,
        string sourceRoot,
        string destRoot,
        ParallelOptions options,
        Action onOverwritten)
    {
        await Parallel.ForEachAsync(
            overwriteFiles,
            options,
            async (relFile, ct) =>
            {
                string srcFull = fileSystem.Combine(sourceRoot, relFile);
                string dstFull = fileSystem.Combine(destRoot, relFile);

                FileEntry meta = sourceFiles[relFile];
                await fileSystem.CopyFileAsync(
                    srcFull,
                    dstFull,
                    overwrite: true,
                    meta.LastWriteTimeUtc,
                    ct);
                onOverwritten();
            });
    }
}

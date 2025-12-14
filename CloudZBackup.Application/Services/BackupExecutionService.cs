using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace CloudZBackup.Application.Services;

public sealed class BackupExecutionService(
    IFileSystemService fileSystem,
    IOptions<BackupOptions> options
) : IBackupExecutionService
{
    public async Task<BackupExecutionStats> ExecuteAsync(
        BackupMode mode,
        Plan plan,
        Snapshot sourceSnapshot,
        string sourceRoot,
        string destRoot,
        IReadOnlyCollection<RelativePath> filesToOverwrite,
        CancellationToken ct
    )
    {
        var ioOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.MaxFileIoConcurrency,
            CancellationToken = ct,
        };

        int createdDirs = 0,
            copied = 0,
            overwritten = 0,
            deletedFiles = 0,
            deletedDirs = 0;

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            await CreateDirectoriesAsync(
                plan.DirsToCreate,
                destRoot,
                ioOptions,
                () => Interlocked.Increment(ref createdDirs)
            );

            await CopyMissingFilesAsync(
                plan.MissingFiles,
                sourceSnapshot.Files,
                sourceRoot,
                destRoot,
                ioOptions,
                () => Interlocked.Increment(ref copied)
            );

            if (mode == BackupMode.Sync && filesToOverwrite.Count > 0)
            {
                await OverwriteFilesAsync(
                    filesToOverwrite,
                    sourceSnapshot.Files,
                    sourceRoot,
                    destRoot,
                    ioOptions,
                    () => Interlocked.Increment(ref overwritten)
                );
            }
        }

        if (mode is BackupMode.Sync or BackupMode.Remove)
        {
            await DeleteExtraFilesAsync(
                plan.ExtraFiles,
                destRoot,
                ioOptions,
                () => Interlocked.Increment(ref deletedFiles)
            );

            foreach (RelativePath relDir in plan.TopLevelExtraDirs)
            {
                ct.ThrowIfCancellationRequested();

                string full = fileSystem.Combine(destRoot, relDir);
                fileSystem.DeleteDirectoryIfExists(full, recursive: true);
                deletedDirs++;
            }
        }

        return new BackupExecutionStats(
            createdDirs,
            copied,
            overwritten,
            deletedFiles,
            deletedDirs
        );
    }

    private async Task CopyMissingFilesAsync(
        List<RelativePath> missingFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> sourceFiles,
        string sourceRoot,
        string destRoot,
        ParallelOptions options,
        Action onCopied
    )
    {
        if (missingFiles.Count == 0)
            return;

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
                    ct
                );
                onCopied();
            }
        );
    }

    private async Task CreateDirectoriesAsync(
        List<RelativePath> dirsToCreate,
        string destRoot,
        ParallelOptions options,
        Action onCreated
    )
    {
        if (dirsToCreate.Count == 0)
            return;

        await Parallel.ForEachAsync(
            dirsToCreate,
            options,
            (relDir, ct) =>
            {
                string full = fileSystem.Combine(destRoot, relDir);
                fileSystem.CreateDirectory(full);
                onCreated();
                return ValueTask.CompletedTask;
            }
        );
    }

    private async Task DeleteExtraFilesAsync(
        List<RelativePath> extraFiles,
        string destRoot,
        ParallelOptions options,
        Action onDeleted
    )
    {
        if (extraFiles.Count == 0)
            return;

        await Parallel.ForEachAsync(
            extraFiles,
            options,
            (relFile, ct) =>
            {
                string dstFull = fileSystem.Combine(destRoot, relFile);
                fileSystem.DeleteFileIfExists(dstFull);
                onDeleted();
                return ValueTask.CompletedTask;
            }
        );
    }

    private async Task OverwriteFilesAsync(
        IReadOnlyCollection<RelativePath> overwriteFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> sourceFiles,
        string sourceRoot,
        string destRoot,
        ParallelOptions options,
        Action onOverwritten
    )
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
                    ct
                );
                onOverwritten();
            }
        );
    }
}

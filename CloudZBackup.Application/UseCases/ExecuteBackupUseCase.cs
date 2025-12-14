using System.Collections.Concurrent;
using CloudZBackup.Application.Abstractions;
using CloudZBackup.Application.UseCases.Options;
using CloudZBackup.Application.UseCases.Request;
using CloudZBackup.Application.UseCases.Result;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudZBackup.Application.UseCases;

public sealed class ExecuteBackupUseCase(
    IFileSystem fileSystem,
    IHashCalculator hashCalculator,
    IOptions<BackupOptions> options,
    ILogger<ExecuteBackupUseCase> logger
    ) : IExecuteBackupUseCase
{
    private readonly BackupOptions _options = options.Value;
    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private readonly IEqualityComparer<RelativePath> _relativePathComparer =
        new RelativePathComparer(OperatingSystem.IsWindows());

    public async Task<BackupResult> ExecuteAsync(
        BackupRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationPath);

        var sourceRoot = NormalizeFullPath(request.SourcePath);
        var destRoot = NormalizeFullPath(request.DestinationPath);

        ValidateNoOverlap(sourceRoot, destRoot);

        if (!fileSystem.DirectoryExists(sourceRoot))
            throw new DirectoryNotFoundException(
                $"Source directory not found: '{sourceRoot}'."
            );

        if (request.Mode is BackupMode.Sync or BackupMode.Add)
        {
            if (!fileSystem.DirectoryExists(destRoot))
                fileSystem.CreateDirectory(destRoot);
        }
        else
        {
            // Remove-only: if destination does not exist, nothing to do.
            if (!fileSystem.DirectoryExists(destRoot))
            {
                logger.LogInformation(
                    "Destination directory does not exist. Nothing to remove."
                );
                return new BackupResult(0, 0, 0, 0, 0);
            }
        }

        logger.LogInformation("Capturing snapshots...");
        var sourceSnapshot = CaptureSnapshot(sourceRoot, cancellationToken);
        var destSnapshot = CaptureSnapshot(destRoot, cancellationToken);

        logger.LogInformation("Planning operations for mode: {Mode}", request.Mode);

        var dirsToCreate = sourceSnapshot
            .Directories.Except(destSnapshot.Directories, _relativePathComparer)
            .OrderBy(p => p.Value.Length)
            .ToList();

        var sourceFiles = sourceSnapshot.Files;
        var destFiles = destSnapshot.Files;

        var filesMissingInDest = sourceFiles
            .Keys.Except(destFiles.Keys, _relativePathComparer)
            .ToList();

        var filesExtraInDest = destFiles
            .Keys.Except(sourceFiles.Keys, _relativePathComparer)
            .ToList();

        var extraDirsInDest = destSnapshot
            .Directories.Except(sourceSnapshot.Directories, _relativePathComparer)
            .ToHashSet(_relativePathComparer);

        var topLevelExtraDirs = ComputeTopLevelDirectories(extraDirsInDest)
            .OrderByDescending(p => p.Value.Length)
            .ToList();

        var filesToOverwrite = new ConcurrentBag<RelativePath>();

        if (request.Mode == BackupMode.Sync)
        {
            var common = sourceFiles
                .Keys.Intersect(destFiles.Keys, _relativePathComparer)
                .ToList();

            logger.LogInformation(
                "Verifying {Count} existing file(s) using SHA-256...",
                common.Count
            );

            await Parallel.ForEachAsync(
                common,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _options.MaxHashConcurrency,
                    CancellationToken = cancellationToken,
                },
                async (relPath, ct) =>
                {
                    var srcFull = Path.Combine(sourceRoot, relPath.ToSystemPath());
                    var dstFull = Path.Combine(destRoot, relPath.ToSystemPath());

                    // If sizes differ, overwrite without hashing both (fastest path).
                    var srcMeta = sourceFiles[relPath];
                    var dstMeta = destFiles[relPath];

                    if (srcMeta.Length != dstMeta.Length)
                    {
                        filesToOverwrite.Add(relPath);
                        return;
                    }

                    // Requirement: verify existing files via SHA-256.
                    var srcHash = await hashCalculator.ComputeSha256HexAsync(srcFull, ct);
                    var dstHash = await hashCalculator.ComputeSha256HexAsync(dstFull, ct);

                    if (!StringComparer.OrdinalIgnoreCase.Equals(srcHash, dstHash))
                        filesToOverwrite.Add(relPath);
                }
            );
        }

        int createdDirs = 0,
            copied = 0,
            overwritten = 0,
            deletedFiles = 0,
            deletedDirs = 0;

        if (request.Mode is BackupMode.Sync or BackupMode.Add)
        {
            // Create missing directories
            await Parallel.ForEachAsync(
                dirsToCreate,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _options.MaxFileIoConcurrency,
                    CancellationToken = cancellationToken,
                },
                (relDir, ct) =>
                {
                    var full = Path.Combine(destRoot, relDir.ToSystemPath());
                    fileSystem.CreateDirectory(full);
                    Interlocked.Increment(ref createdDirs);
                    return ValueTask.CompletedTask;
                }
            );

            // Copy missing files
            await Parallel.ForEachAsync(
                filesMissingInDest,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _options.MaxFileIoConcurrency,
                    CancellationToken = cancellationToken,
                },
                async (relFile, ct) =>
                {
                    var srcFull = Path.Combine(sourceRoot, relFile.ToSystemPath());
                    var dstFull = Path.Combine(destRoot, relFile.ToSystemPath());

                    var meta = sourceFiles[relFile];
                    await fileSystem.CopyFileAsync(
                        srcFull,
                        dstFull,
                        overwrite: false,
                        meta.LastWriteTimeUtc,
                        ct
                    );
                    Interlocked.Increment(ref copied);
                }
            );

            // Overwrite changed files (Sync only)
            if (request.Mode == BackupMode.Sync && !filesToOverwrite.IsEmpty)
            {
                var overwriteList = filesToOverwrite.ToList();

                await Parallel.ForEachAsync(
                    overwriteList,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = _options.MaxFileIoConcurrency,
                        CancellationToken = cancellationToken,
                    },
                    async (relFile, ct) =>
                    {
                        var srcFull = Path.Combine(sourceRoot, relFile.ToSystemPath());
                        var dstFull = Path.Combine(destRoot, relFile.ToSystemPath());

                        var meta = sourceFiles[relFile];
                        await fileSystem.CopyFileAsync(
                            srcFull,
                            dstFull,
                            overwrite: true,
                            meta.LastWriteTimeUtc,
                            ct
                        );
                        Interlocked.Increment(ref overwritten);
                    }
                );
            }
        }

        if (request.Mode is BackupMode.Sync or BackupMode.Remove)
        {
            // Delete extra files
            await Parallel.ForEachAsync(
                filesExtraInDest,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = _options.MaxFileIoConcurrency,
                    CancellationToken = cancellationToken,
                },
                (relFile, ct) =>
                {
                    var dstFull = Path.Combine(destRoot, relFile.ToSystemPath());
                    fileSystem.DeleteFileIfExists(dstFull);
                    Interlocked.Increment(ref deletedFiles);
                    return ValueTask.CompletedTask;
                }
            );

            // Delete extra directories (top-level only), recursive.
            foreach (var relDir in topLevelExtraDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var full = Path.Combine(destRoot, relDir.ToSystemPath());
                fileSystem.DeleteDirectoryIfExists(full, recursive: true);
                deletedDirs++;
            }
        }

        return new BackupResult(createdDirs, copied, overwritten, deletedFiles, deletedDirs);
    }

    private Snapshot CaptureSnapshot(string rootPath, CancellationToken ct)
    {
        var files = new Dictionary<RelativePath, FileEntry>(_relativePathComparer);
        var dirs = new HashSet<RelativePath>(_relativePathComparer);

        foreach (var dir in fileSystem.EnumerateDirectoriesRecursive(rootPath))
        {
            ct.ThrowIfCancellationRequested();
            var rel = RelativePath.FromSystem(Path.GetRelativePath(rootPath, dir));
            if (!string.IsNullOrEmpty(rel.Value))
                dirs.Add(rel);
        }

        foreach (var file in fileSystem.EnumerateFilesRecursive(rootPath))
        {
            ct.ThrowIfCancellationRequested();
            var rel = RelativePath.FromSystem(Path.GetRelativePath(rootPath, file));
            var meta = fileSystem.GetFileMetadata(file);
            files[rel] = new FileEntry(rel, meta.Length, meta.LastWriteTimeUtc);
        }

        return new Snapshot(files, dirs);
    }

    private void ValidateNoOverlap(string sourceRoot, string destRoot)
    {
        // Avoid pathological cases like destination inside source (causes self-replication during sync/add).
        var src = EnsureTrailingSeparator(sourceRoot);
        var dst = EnsureTrailingSeparator(destRoot);

        if (dst.StartsWith(src, _pathComparison))
            throw new InvalidOperationException(
                "Destination cannot be located inside the source directory."
            );

        if (src.StartsWith(dst, _pathComparison))
            throw new InvalidOperationException(
                "Source cannot be located inside the destination directory."
            );
    }

    private static string NormalizeFullPath(string path) =>
        Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string EnsureTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;

    private static List<RelativePath> ComputeTopLevelDirectories(HashSet<RelativePath> extras)
    {
        var result = new List<RelativePath>();

        foreach (var dir in extras)
        {
            if (!HasAncestorInSet(dir, extras))
                result.Add(dir);
        }

        return result;

        static bool HasAncestorInSet(RelativePath dir, HashSet<RelativePath> set)
        {
            var value = dir.Value;

            while (true)
            {
                var idx = value.LastIndexOf('/');
                if (idx <= 0)
                    return false;

                value = value[..idx];
                if (set.Contains(new RelativePath(value)))
                    return true;
            }
        }
    }

    private sealed class RelativePathComparer(bool ignoreCase) : IEqualityComparer<RelativePath>
    {
        private readonly StringComparer _comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        public bool Equals(RelativePath x, RelativePath y) =>
            _comparer.Equals(x.Value, y.Value);

        public int GetHashCode(RelativePath obj) => _comparer.GetHashCode(obj.Value);
    }
}

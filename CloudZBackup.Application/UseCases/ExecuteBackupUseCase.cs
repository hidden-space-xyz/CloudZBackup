using System.Collections.Concurrent;
using CloudZBackup.Application.Abstractions.FileSystem;
using CloudZBackup.Application.Abstractions.Hashing;
using CloudZBackup.Application.Abstractions.UseCases;
using CloudZBackup.Application.Comparers;
using CloudZBackup.Application.UseCases.Options;
using CloudZBackup.Application.UseCases.Request;
using CloudZBackup.Application.UseCases.Result;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudZBackup.Application.UseCases;

public sealed partial class ExecuteBackupUseCase(
    IFileSystem fileSystem,
    IHashCalculator hashCalculator,
    IOptions<BackupOptions> options,
    ILogger<ExecuteBackupUseCase> logger
) : IExecuteBackupUseCase
{
    private readonly BackupOptions options = options.Value;

    private readonly StringComparison pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private readonly IEqualityComparer<RelativePath> relativePathComparer =
        new RelativePathComparer(OperatingSystem.IsWindows());

    public async Task<BackupResult> ExecuteAsync(
        BackupRequest request,
        CancellationToken cancellationToken
    )
    {
        (string sourceRoot, string destRoot) = ValidateAndNormalize(request);

        ValidateNoOverlap(sourceRoot, destRoot);

        EnsureSourceExists(sourceRoot);

        bool destWasCreated = PrepareDestination(request.Mode, destRoot);

        logger.LogInformation("Capturing snapshots...");

        bool needSourceMeta = request.Mode is BackupMode.Sync or BackupMode.Add;
        bool needDestMeta = request.Mode is BackupMode.Sync;

        Snapshot sourceSnapshot = CaptureSnapshot(sourceRoot, needSourceMeta, cancellationToken);

        Snapshot destSnapshot = destWasCreated
            ? CreateEmptySnapshot()
            : CaptureSnapshot(destRoot, needDestMeta, cancellationToken);

        logger.LogInformation("Planning operations for mode: {Mode}", request.Mode);

        Plan plan = BuildPlan(request.Mode, sourceSnapshot, destSnapshot);

        List<RelativePath> filesToOverwrite = [];

        if (request.Mode == BackupMode.Sync && plan.CommonFiles.Count > 0)
        {
            logger.LogInformation(
                "Verifying {Count} existing file(s) using SHA-256...",
                plan.CommonFiles.Count
            );

            filesToOverwrite = await ComputeFilesToOverwriteAsync(
                plan.CommonFiles,
                sourceSnapshot.Files,
                destSnapshot.Files,
                sourceRoot,
                destRoot,
                cancellationToken
            );
        }

        var ioOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxFileIoConcurrency,
            CancellationToken = cancellationToken,
        };

        int createdDirs = 0,
            copied = 0,
            overwritten = 0,
            deletedFiles = 0,
            deletedDirs = 0;

        if (request.Mode is BackupMode.Sync or BackupMode.Add)
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

            if (request.Mode == BackupMode.Sync && filesToOverwrite.Count > 0)
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

        if (request.Mode is BackupMode.Sync or BackupMode.Remove)
        {
            await DeleteExtraFilesAsync(
                plan.ExtraFiles,
                destRoot,
                ioOptions,
                () => Interlocked.Increment(ref deletedFiles)
            );

            foreach (RelativePath relDir in plan.TopLevelExtraDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string full = Combine(destRoot, relDir);
                fileSystem.DeleteDirectoryIfExists(full, recursive: true);
                deletedDirs++;
            }
        }

        return new BackupResult(createdDirs, copied, overwritten, deletedFiles, deletedDirs);
    }

    private static (string sourceRoot, string destRoot) ValidateAndNormalize(BackupRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationPath);

        string sourceRoot = NormalizeFullPath(request.SourcePath);
        string destRoot = NormalizeFullPath(request.DestinationPath);

        return (sourceRoot, destRoot);
    }

    private void EnsureSourceExists(string sourceRoot)
    {
        if (!fileSystem.DirectoryExists(sourceRoot))
            throw new DirectoryNotFoundException($"Source directory not found: '{sourceRoot}'.");
    }

    private bool PrepareDestination(BackupMode mode, string destRoot)
    {
        bool destExists = fileSystem.DirectoryExists(destRoot);

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            if (!destExists)
            {
                fileSystem.CreateDirectory(destRoot);
                return true;
            }

            return false;
        }

        if (!destExists)
        {
            logger.LogInformation("Destination directory does not exist. Nothing to remove.");
            throw new DirectoryNotFoundException();
        }

        return false;
    }

    private Snapshot CreateEmptySnapshot()
    {
        return new Snapshot(
            new Dictionary<RelativePath, FileEntry>(relativePathComparer),
            new HashSet<RelativePath>(relativePathComparer)
        );
    }

    private Plan BuildPlan(BackupMode mode, Snapshot source, Snapshot dest)
    {
        var dirsToCreate = new List<RelativePath>();
        var missingFiles = new List<RelativePath>();
        var commonFiles = new List<RelativePath>();
        var extraFiles = new List<RelativePath>();
        var topLevelExtraDirs = new List<RelativePath>();

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            foreach (RelativePath dir in source.Directories)
            {
                if (!dest.Directories.Contains(dir))
                    dirsToCreate.Add(dir);
            }

            dirsToCreate.Sort((a, b) => a.Value.Length.CompareTo(b.Value.Length));

            foreach (RelativePath file in source.Files.Keys)
            {
                if (!dest.Files.ContainsKey(file))
                    missingFiles.Add(file);
                else if (mode == BackupMode.Sync)
                    commonFiles.Add(file);
            }
        }

        if (mode is BackupMode.Sync or BackupMode.Remove)
        {
            foreach (RelativePath file in dest.Files.Keys)
            {
                if (!source.Files.ContainsKey(file))
                    extraFiles.Add(file);
            }

            var extraDirs = new HashSet<RelativePath>(relativePathComparer);
            foreach (RelativePath dir in dest.Directories)
            {
                if (!source.Directories.Contains(dir))
                    extraDirs.Add(dir);
            }

            topLevelExtraDirs = ComputeTopLevelDirectories(extraDirs);
        }

        return new Plan(dirsToCreate, missingFiles, commonFiles, extraFiles, topLevelExtraDirs);
    }

    private async Task<List<RelativePath>> ComputeFilesToOverwriteAsync(
        List<RelativePath> commonFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> sourceFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> destFiles,
        string sourceRoot,
        string destRoot,
        CancellationToken ct
    )
    {
        var bag = new ConcurrentBag<RelativePath>();

        var hashOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxHashConcurrency,
            CancellationToken = ct,
        };

        await Parallel.ForEachAsync(
            commonFiles,
            hashOptions,
            async (relPath, token) =>
            {
                string srcFull = Combine(sourceRoot, relPath);
                string dstFull = Combine(destRoot, relPath);

                FileEntry srcMeta = sourceFiles[relPath];
                FileEntry dstMeta = destFiles[relPath];

                if (srcMeta.Length != dstMeta.Length)
                {
                    bag.Add(relPath);
                    return;
                }

                byte[] srcHash = await hashCalculator.ComputeSha256Async(srcFull, token);
                byte[] dstHash = await hashCalculator.ComputeSha256Async(dstFull, token);

                if (!srcHash.SequenceEqual(dstHash))
                    bag.Add(relPath);
            }
        );

        return bag.ToList();
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
                string full = Combine(destRoot, relDir);
                fileSystem.CreateDirectory(full);
                onCreated();
                return ValueTask.CompletedTask;
            }
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
                string srcFull = Combine(sourceRoot, relFile);
                string dstFull = Combine(destRoot, relFile);

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

    private async Task OverwriteFilesAsync(
        List<RelativePath> overwriteFiles,
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
                string srcFull = Combine(sourceRoot, relFile);
                string dstFull = Combine(destRoot, relFile);

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
                string dstFull = Combine(destRoot, relFile);
                fileSystem.DeleteFileIfExists(dstFull);
                onDeleted();
                return ValueTask.CompletedTask;
            }
        );
    }

    private Snapshot CaptureSnapshot(
        string rootPath,
        bool includeFileMetadata,
        CancellationToken ct
    )
    {
        Dictionary<RelativePath, FileEntry> files = new(relativePathComparer);
        HashSet<RelativePath> dirs = new(relativePathComparer);

        foreach (string dir in fileSystem.EnumerateDirectoriesRecursive(rootPath))
        {
            ct.ThrowIfCancellationRequested();
            RelativePath rel = RelativePath.FromSystem(Path.GetRelativePath(rootPath, dir));
            if (!string.IsNullOrEmpty(rel.Value))
                dirs.Add(rel);
        }

        foreach (string file in fileSystem.EnumerateFilesRecursive(rootPath))
        {
            ct.ThrowIfCancellationRequested();
            RelativePath rel = RelativePath.FromSystem(Path.GetRelativePath(rootPath, file));

            if (includeFileMetadata)
            {
                FileMetadata meta = fileSystem.GetFileMetadata(file);
                files[rel] = new FileEntry(rel, meta.Length, meta.LastWriteTimeUtc);
            }
            else
            {
                files[rel] = new FileEntry(rel, 0, default);
            }
        }

        return new Snapshot(files, dirs);
    }

    private void ValidateNoOverlap(string sourceRoot, string destRoot)
    {
        string src = EnsureTrailingSeparator(sourceRoot);
        string dst = EnsureTrailingSeparator(destRoot);

        if (dst.StartsWith(src, pathComparison))
            throw new InvalidOperationException(
                "Destination cannot be located inside the source directory."
            );

        if (src.StartsWith(dst, pathComparison))
            throw new InvalidOperationException(
                "Source cannot be located inside the destination directory."
            );
    }

    private static string NormalizeFullPath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string EnsureTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;

    private static string Combine(string root, RelativePath rel) =>
        Path.Combine(root, rel.ToSystemPath());

    private static List<RelativePath> ComputeTopLevelDirectories(HashSet<RelativePath> extras)
    {
        return extras.Where(d => !HasAncestorInSet(d, extras)).ToList();

        static bool HasAncestorInSet(RelativePath dir, HashSet<RelativePath> set)
        {
            string value = dir.Value;

            while (true)
            {
                int idx = value.LastIndexOf('/');
                if (idx <= 0)
                    return false;

                value = value[..idx];
                if (set.Contains(new RelativePath(value)))
                    return true;
            }
        }
    }
}

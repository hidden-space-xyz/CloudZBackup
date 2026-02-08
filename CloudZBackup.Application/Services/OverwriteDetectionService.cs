using System.Collections.Concurrent;
using System.Security.Cryptography;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace CloudZBackup.Application.Services;

/// <summary>
/// Identifies which files among a set of common (source ∩ destination) files have changed
/// by comparing file sizes, timestamps, and SHA-256 hashes.
/// Files that match in both size and last-write time are assumed unchanged,
/// avoiding expensive hash computation.
/// </summary>
public sealed class OverwriteDetectionService(
    IHashingService hashCalculator,
    IOptions<BackupOptions> options,
    IFileSystemService fileSystemService
) : IOverwriteDetectionService
{
    /// <inheritdoc />
    public async Task<List<RelativePath>> ComputeFilesToOverwriteAsync(
        IReadOnlyList<RelativePath> commonFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> sourceFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> destFiles,
        string sourceRoot,
        string destRoot,
        CancellationToken ct
    )
    {
        var bag = new ConcurrentQueue<RelativePath>();

        var hashOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Value.MaxHashConcurrency,
            CancellationToken = ct,
        };

        await Parallel.ForEachAsync(
            commonFiles,
            hashOptions,
            async (relPath, token) =>
            {
                FileEntry srcMeta = sourceFiles[relPath];
                FileEntry dstMeta = destFiles[relPath];

                if (srcMeta.Length != dstMeta.Length)
                {
                    bag.Enqueue(relPath);
                    return;
                }

                if (srcMeta.LastWriteTimeUtc == dstMeta.LastWriteTimeUtc)
                    return;

                string srcFull = fileSystemService.Combine(sourceRoot, relPath);
                string dstFull = fileSystemService.Combine(destRoot, relPath);

                byte[] srcHash = await hashCalculator.ComputeSha256Async(srcFull, token);
                byte[] dstHash = await hashCalculator.ComputeSha256Async(dstFull, token);

                if (!CryptographicOperations.FixedTimeEquals(srcHash, dstHash))
                    bag.Enqueue(relPath);
            }
        );

        return [.. bag];
    }
}

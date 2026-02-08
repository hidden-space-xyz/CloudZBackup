using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace CloudZBackup.Application.Services;

/// <summary>
/// Identifies which files among a set of common (source ∩ destination) files have changed
/// by comparing file sizes and SHA-256 hashes.
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
        var bag = new ConcurrentBag<RelativePath>();

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
                string srcFull = fileSystemService.Combine(sourceRoot, relPath);
                string dstFull = fileSystemService.Combine(destRoot, relPath);

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
}

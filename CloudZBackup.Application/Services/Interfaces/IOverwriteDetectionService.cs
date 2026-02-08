using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services.Interfaces;

/// <summary>
/// Determines which files among a set of common (shared) files have changed
/// and therefore need to be overwritten during a sync operation.
/// </summary>
public interface IOverwriteDetectionService
{
    /// <summary>
    /// Compares common files between source and destination using size and SHA-256 hashes
    /// to identify files that require overwriting.
    /// </summary>
    /// <param name="commonFiles">The list of relative paths present in both source and destination.</param>
    /// <param name="sourceFiles">File metadata from the source snapshot.</param>
    /// <param name="destFiles">File metadata from the destination snapshot.</param>
    /// <param name="sourceRoot">The absolute path to the source root directory.</param>
    /// <param name="destRoot">The absolute path to the destination root directory.</param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A list of relative paths for files that need to be overwritten.</returns>
    Task<List<RelativePath>> ComputeFilesToOverwriteAsync(
        IReadOnlyList<RelativePath> commonFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> sourceFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> destFiles,
        string sourceRoot,
        string destRoot,
        CancellationToken ct
    );
}

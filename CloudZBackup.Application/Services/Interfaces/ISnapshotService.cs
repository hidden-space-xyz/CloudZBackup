namespace CloudZBackup.Application.Services.Interfaces;

using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Captures point-in-time snapshots of directory trees for comparison during backup planning.
/// </summary>
public interface ISnapshotService
{
    /// <summary>
    /// Recursively scans the specified directory and returns a <see cref="Snapshot"/> of its contents.
    /// </summary>
    /// <param name="rootPath">The absolute path to the root directory to scan.</param>
    /// <param name="includeFileMetadata">
    /// <see langword="true"/> to capture file size and last-write time;
    /// <see langword="false"/> to capture paths only.
    /// </param>
    /// <param name="ct">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="Snapshot"/> representing the directory tree.</returns>
    Snapshot CaptureSnapshot(string rootPath, bool includeFileMetadata, CancellationToken ct);

    /// <summary>
    /// Creates an empty <see cref="Snapshot"/> with no files or directories.
    /// </summary>
    /// <returns>An empty snapshot.</returns>
    Snapshot CreateEmptySnapshot();
}

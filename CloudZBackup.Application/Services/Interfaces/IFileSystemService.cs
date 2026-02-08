namespace CloudZBackup.Application.Services.Interfaces;

using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Abstracts file-system operations so that the application layer remains
/// independent of the underlying storage mechanism.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Combines a root directory path with a <see cref="RelativePath"/> to produce an absolute path.
    /// </summary>
    /// <param name="root">The root directory path.</param>
    /// <param name="rel">The relative path to append.</param>
    /// <returns>The combined absolute path.</returns>
    string Combine(string root, RelativePath rel);

    /// <summary>
    /// Asynchronously copies a file from <paramref name="sourceFile"/> to <paramref name="destinationFile"/>.
    /// </summary>
    /// <param name="sourceFile">The absolute path to the source file.</param>
    /// <param name="destinationFile">The absolute path to the destination file.</param>
    /// <param name="overwrite"><see langword="true"/> to overwrite an existing destination file.</param>
    /// <param name="lastWriteTimeUtc">If set, the last-write time to apply to the destination file.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CopyFileAsync(
        string sourceFile,
        string destinationFile,
        bool overwrite,
        DateTime? lastWriteTimeUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates the specified directory (and any intermediate directories) if it does not already exist.
    /// </summary>
    /// <param name="path">The absolute path of the directory to create.</param>
    void CreateDirectory(string path);

    /// <summary>
    /// Deletes the specified directory if it exists.
    /// </summary>
    /// <param name="directoryPath">The absolute path of the directory to delete.</param>
    /// <param name="recursive"><see langword="true"/> to delete contents recursively.</param>
    void DeleteDirectoryIfExists(string directoryPath, bool recursive);

    /// <summary>
    /// Deletes the specified file if it exists.
    /// </summary>
    /// <param name="filePath">The absolute path of the file to delete.</param>
    void DeleteFileIfExists(string filePath);

    /// <summary>
    /// Determines whether the specified directory exists.
    /// </summary>
    /// <param name="path">The absolute path to check.</param>
    /// <returns><see langword="true"/> if the directory exists; otherwise, <see langword="false"/>.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Ensures the source directory exists; throws <see cref="DirectoryNotFoundException"/> otherwise.
    /// </summary>
    /// <param name="sourceRoot">The absolute path to the source root directory.</param>
    void EnsureSourceExists(string sourceRoot);

    /// <summary>
    /// Enumerates all directories recursively under the specified root.
    /// </summary>
    /// <param name="rootPath">The root path to enumerate from.</param>
    /// <returns>An enumerable of absolute directory paths.</returns>
    IEnumerable<string> EnumerateDirectoriesRecursive(string rootPath);

    /// <summary>
    /// Enumerates all files recursively under the specified root.
    /// </summary>
    /// <param name="rootPath">The root path to enumerate from.</param>
    /// <returns>An enumerable of absolute file paths.</returns>
    IEnumerable<string> EnumerateFilesRecursive(string rootPath);

    /// <summary>
    /// Retrieves basic metadata for the specified file.
    /// </summary>
    /// <param name="filePath">The absolute path of the file.</param>
    /// <returns>A <see cref="FileMetadata"/> instance containing size and timestamp.</returns>
    FileMetadata GetFileMetadata(string filePath);

    /// <summary>
    /// Prepares the destination directory for the backup, creating it if necessary.
    /// </summary>
    /// <param name="mode">The backup mode.</param>
    /// <param name="destRoot">The absolute path to the destination root.</param>
    /// <returns><see langword="true"/> if the destination was newly created; otherwise, <see langword="false"/>.</returns>
    bool PrepareDestination(BackupMode mode, string destRoot);

    /// <summary>
    /// Validates and normalizes the source and destination paths from a <see cref="BackupRequest"/>.
    /// </summary>
    /// <param name="request">The backup request containing raw paths.</param>
    /// <returns>A tuple of the normalized source and destination root paths.</returns>
    (string SourceRoot, string DestRoot) ValidateAndNormalize(BackupRequest request);

    /// <summary>
    /// Validates that the source and destination paths do not overlap.
    /// </summary>
    /// <param name="sourceRoot">The normalized source root path.</param>
    /// <param name="destRoot">The normalized destination root path.</param>
    void ValidateNoOverlap(string sourceRoot, string destRoot);
}

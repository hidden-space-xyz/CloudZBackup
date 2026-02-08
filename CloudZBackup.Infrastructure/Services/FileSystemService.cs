using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Infrastructure.Services;

/// <summary>
/// Concrete implementation of <see cref="IFileSystemService"/> backed by the local file system.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <inheritdoc />
    public string Combine(string root, RelativePath rel)
    {
        return Path.Combine(root, rel.ToSystemPath());
    }

    /// <inheritdoc />
    public async Task CopyFileAsync(
        string sourceFile,
        string destinationFile,
        bool overwrite,
        DateTime? lastWriteTimeUtc,
        CancellationToken cancellationToken
    )
    {
        string? destDir = Path.GetDirectoryName(destinationFile);

        if (!string.IsNullOrWhiteSpace(destDir))
            Directory.CreateDirectory(destDir);

        FileMode destMode = overwrite ? FileMode.Create : FileMode.CreateNew;

        await using FileStream source = new(
            sourceFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.SequentialScan
        );

        await using FileStream dest = new(
            destinationFile,
            destMode,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            options: FileOptions.SequentialScan
        );

        await source.CopyToAsync(dest, bufferSize: 1024 * 1024, cancellationToken);

        if (lastWriteTimeUtc.HasValue)
            File.SetLastWriteTimeUtc(destinationFile, lastWriteTimeUtc.Value);
    }

    /// <inheritdoc />
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    /// <inheritdoc />
    public void DeleteDirectoryIfExists(string directoryPath, bool recursive)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive);
    }

    /// <inheritdoc />
    public void DeleteFileIfExists(string filePath)
    {
        File.Delete(filePath);
    }

    /// <inheritdoc />
    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    /// <inheritdoc />
    public void EnsureSourceExists(string sourceRoot)
    {
        if (!DirectoryExists(sourceRoot))
            throw new DirectoryNotFoundException($"Source directory not found: '{sourceRoot}'.");
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateDirectoriesRecursive(string rootPath)
    {
        return Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories);
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFilesRecursive(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories);
    }

    /// <inheritdoc />
    public FileMetadata GetFileMetadata(string filePath)
    {
        FileInfo info = new(filePath);
        return new FileMetadata(info.Length, info.LastWriteTimeUtc);
    }

    /// <inheritdoc />
    public bool PrepareDestination(BackupMode mode, string destRoot)
    {
        bool destExists = DirectoryExists(destRoot);

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            if (!destExists)
            {
                CreateDirectory(destRoot);
                return true;
            }

            return false;
        }

        return !destExists;
    }

    /// <inheritdoc />
    public (string sourceRoot, string destRoot) ValidateAndNormalize(BackupRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationPath);

        string sourceRoot = NormalizeFullPath(request.SourcePath);
        string destRoot = NormalizeFullPath(request.DestinationPath);

        return (sourceRoot, destRoot);
    }

    /// <inheritdoc />
    public void ValidateNoOverlap(string sourceRoot, string destRoot)
    {
        string src = EnsureTrailingSeparator(sourceRoot);
        string dst = EnsureTrailingSeparator(destRoot);

        if (dst.StartsWith(src, _pathComparison))
            throw new InvalidOperationException(
                "Destination cannot be located inside the source directory."
            );

        if (src.StartsWith(dst, _pathComparison))
            throw new InvalidOperationException(
                "Source cannot be located inside the destination directory."
            );
    }

    /// <summary>
    /// Appends a trailing directory separator to ensure correct prefix comparison.
    /// </summary>
    private static string EnsureTrailingSeparator(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Resolves a path to its fully-qualified form and strips trailing separators.
    /// </summary>
    private static string NormalizeFullPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Infrastructure.Services;

public sealed class FileSystemService : IFileSystemService
{
    private readonly StringComparison pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public string Combine(string root, RelativePath rel)
    {
        return Path.Combine(root, rel.ToSystemPath());
    }

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

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void DeleteDirectoryIfExists(string directoryPath, bool recursive)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive);
    }

    public void DeleteFileIfExists(string filePath)
    {
        File.Delete(filePath);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public void EnsureSourceExists(string sourceRoot)
    {
        if (!DirectoryExists(sourceRoot))
            throw new DirectoryNotFoundException($"Source directory not found: '{sourceRoot}'.");
    }

    public IEnumerable<string> EnumerateDirectoriesRecursive(string rootPath)
    {
        return Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories);
    }

    public IEnumerable<string> EnumerateFilesRecursive(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories);
    }

    public FileMetadata GetFileMetadata(string filePath)
    {
        FileInfo info = new(filePath);
        return new FileMetadata(info.Length, info.LastWriteTimeUtc);
    }

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

        // Remove-only mode requires destination to exist
        if (!destExists)
        {
            return true;
        }

        return false;
    }

    public (string sourceRoot, string destRoot) ValidateAndNormalize(BackupRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationPath);

        string sourceRoot = NormalizeFullPath(request.SourcePath);
        string destRoot = NormalizeFullPath(request.DestinationPath);

        return (sourceRoot, destRoot);
    }

    public void ValidateNoOverlap(string sourceRoot, string destRoot)
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

    private static string EnsureTrailingSeparator(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    private static string NormalizeFullPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

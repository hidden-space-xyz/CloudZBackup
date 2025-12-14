using CloudZBackup.Application.Abstractions.FileSystem;
using CloudZBackup.Application.ValueObjects;

namespace CloudZBackup.Infrastructure.FileSystem;

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public IEnumerable<string> EnumerateFilesRecursive(string rootPath)
    {
        return Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories);
    }

    public IEnumerable<string> EnumerateDirectoriesRecursive(string rootPath)
    {
        return Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories);
    }

    public FileMetadata GetFileMetadata(string filePath)
    {
        FileInfo info = new(filePath);
        return new FileMetadata(info.Length, info.LastWriteTimeUtc);
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

    public void DeleteFileIfExists(string filePath)
    {
        File.Delete(filePath);
    }

    public void DeleteDirectoryIfExists(string directoryPath, bool recursive)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive);
    }
}

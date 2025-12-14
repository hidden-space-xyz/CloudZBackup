using CloudZBackup.Application.Abstractions;
using CloudZBackup.Application.ValueObjects;

namespace CloudZBackup.Infrastructure.Implementations;

public sealed class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public IEnumerable<string> EnumerateFilesRecursive(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories);

    public IEnumerable<string> EnumerateDirectoriesRecursive(string rootPath) =>
        Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories);

    public FileMetadata GetFileMetadata(string filePath)
    {
        var info = new FileInfo(filePath);
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
        var destDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrWhiteSpace(destDir))
            Directory.CreateDirectory(destDir);

        var destMode = overwrite ? FileMode.Create : FileMode.CreateNew;

        await using var source = new FileStream(
            sourceFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.SequentialScan
        );

        await using var dest = new FileStream(
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
        // File.Delete does not throw if the file does not exist.
        File.Delete(filePath);
    }

    public void DeleteDirectoryIfExists(string directoryPath, bool recursive)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive);
    }
}

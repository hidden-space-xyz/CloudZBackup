using CloudZBackup.Application.ValueObjects;

namespace CloudZBackup.Application.Abstractions;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);

    IEnumerable<string> EnumerateFilesRecursive(string rootPath);
    IEnumerable<string> EnumerateDirectoriesRecursive(string rootPath);

    FileMetadata GetFileMetadata(string filePath);

    Task CopyFileAsync(
        string sourceFile,
        string destinationFile,
        bool overwrite,
        DateTime? lastWriteTimeUtc,
        CancellationToken cancellationToken
    );

    void DeleteFileIfExists(string filePath);

    void DeleteDirectoryIfExists(string directoryPath, bool recursive);
}

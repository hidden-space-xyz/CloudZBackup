using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services.Interfaces;

public interface IFileSystemService
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

    (string sourceRoot, string destRoot) ValidateAndNormalize(BackupRequest request);
    void ValidateNoOverlap(string sourceRoot, string destRoot);
    string Combine(string root, RelativePath rel);
}

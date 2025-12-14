using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services.Interfaces;

public interface IFileSystemService
{
    string Combine(string root, RelativePath rel);

    Task CopyFileAsync(
        string sourceFile,
        string destinationFile,
        bool overwrite,
        DateTime? lastWriteTimeUtc,
        CancellationToken cancellationToken
    );

    void CreateDirectory(string path);

    void DeleteDirectoryIfExists(string directoryPath, bool recursive);

    void DeleteFileIfExists(string filePath);

    bool DirectoryExists(string path);

    void EnsureSourceExists(string sourceRoot);

    IEnumerable<string> EnumerateDirectoriesRecursive(string rootPath);

    IEnumerable<string> EnumerateFilesRecursive(string rootPath);

    FileMetadata GetFileMetadata(string filePath);

    bool PrepareDestination(BackupMode mode, string destRoot);

    (string sourceRoot, string destRoot) ValidateAndNormalize(BackupRequest request);

    void ValidateNoOverlap(string sourceRoot, string destRoot);
}

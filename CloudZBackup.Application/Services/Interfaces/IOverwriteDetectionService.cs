using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services.Interfaces;

public interface IOverwriteDetectionService
{
    Task<List<RelativePath>> ComputeFilesToOverwriteAsync(
        List<RelativePath> commonFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> sourceFiles,
        IReadOnlyDictionary<RelativePath, FileEntry> destFiles,
        string sourceRoot,
        string destRoot,
        CancellationToken ct
    );
}

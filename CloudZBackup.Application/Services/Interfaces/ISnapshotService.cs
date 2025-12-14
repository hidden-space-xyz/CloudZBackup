using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services.Interfaces;

public interface ISnapshotService
{
    Snapshot CaptureSnapshot(string rootPath, bool includeFileMetadata, CancellationToken ct);
    Snapshot CreateEmptySnapshot();
}

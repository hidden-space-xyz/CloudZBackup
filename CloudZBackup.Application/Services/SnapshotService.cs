using CloudZBackup.Application.Comparers;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services;

public sealed class SnapshotService(IFileSystemService fileSystem) : ISnapshotService
{
    private readonly IEqualityComparer<RelativePath> relativePathComparer =
        new RelativePathComparer(OperatingSystem.IsWindows());

    public Snapshot CaptureSnapshot(string rootPath, bool includeFileMetadata, CancellationToken ct)
    {
        Dictionary<RelativePath, FileEntry> files = new(relativePathComparer);
        HashSet<RelativePath> dirs = new(relativePathComparer);

        foreach (string dir in fileSystem.EnumerateDirectoriesRecursive(rootPath))
        {
            ct.ThrowIfCancellationRequested();
            RelativePath rel = RelativePath.FromSystem(Path.GetRelativePath(rootPath, dir));
            if (!string.IsNullOrEmpty(rel.Value))
                dirs.Add(rel);
        }

        foreach (string file in fileSystem.EnumerateFilesRecursive(rootPath))
        {
            ct.ThrowIfCancellationRequested();
            RelativePath rel = RelativePath.FromSystem(Path.GetRelativePath(rootPath, file));

            if (includeFileMetadata)
            {
                FileMetadata meta = fileSystem.GetFileMetadata(file);
                files[rel] = new FileEntry(rel, meta.Length, meta.LastWriteTimeUtc);
            }
            else
            {
                files[rel] = new FileEntry(rel, 0, default);
            }
        }

        return new Snapshot(files, dirs);
    }

    public Snapshot CreateEmptySnapshot()
    {
        return new Snapshot(
            new Dictionary<RelativePath, FileEntry>(relativePathComparer),
            new HashSet<RelativePath>(relativePathComparer)
        );
    }
}

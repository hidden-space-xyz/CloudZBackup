namespace CloudZBackup.Application.Services;

using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Comparers;
using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Captures directory-tree snapshots by enumerating files and directories through
/// the <see cref="IFileSystemService"/> abstraction.
/// </summary>
public sealed class SnapshotService(IFileSystemService fileSystem) : ISnapshotService
{
    private readonly IEqualityComparer<RelativePath> relativePathComparer =
        new RelativePathComparer(OperatingSystem.IsWindows());

    /// <inheritdoc />
    public Snapshot CaptureSnapshot(string rootPath, bool includeFileMetadata, CancellationToken ct)
    {
        Dictionary<RelativePath, FileEntry> files = new(this.relativePathComparer);
        HashSet<RelativePath> dirs = new(this.relativePathComparer);

        int cancellationCheckCounter = 0;

        foreach (string dir in fileSystem.EnumerateDirectoriesRecursive(rootPath))
        {
            if ((++cancellationCheckCounter & 0xFF) == 0)
            {
                ct.ThrowIfCancellationRequested();
            }

            RelativePath rel = RelativePath.FromSystem(Path.GetRelativePath(rootPath, dir));
            if (!string.IsNullOrEmpty(rel.Value))
            {
                dirs.Add(rel);
            }
        }

        cancellationCheckCounter = 0;

        foreach (string file in fileSystem.EnumerateFilesRecursive(rootPath))
        {
            if ((++cancellationCheckCounter & 0xFF) == 0)
            {
                ct.ThrowIfCancellationRequested();
            }

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

    /// <inheritdoc />
    public Snapshot CreateEmptySnapshot()
    {
        return new Snapshot(
            new Dictionary<RelativePath, FileEntry>(this.relativePathComparer),
            new HashSet<RelativePath>(this.relativePathComparer));
    }
}

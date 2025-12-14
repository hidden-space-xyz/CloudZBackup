namespace CloudZBackup.Domain.ValueObjects;

public sealed record Snapshot(
    IReadOnlyDictionary<RelativePath, FileEntry> Files,
    IReadOnlySet<RelativePath> Directories
);

namespace CloudZBackup.Domain.ValueObjects;

public sealed record FileEntry(RelativePath Path, long Length, DateTime LastWriteTimeUtc);

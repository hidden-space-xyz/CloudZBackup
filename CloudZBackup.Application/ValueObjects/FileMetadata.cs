namespace CloudZBackup.Application.ValueObjects;

public sealed record FileMetadata(long Length, DateTime LastWriteTimeUtc);

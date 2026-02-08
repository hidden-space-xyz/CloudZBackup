namespace CloudZBackup.Application.ValueObjects;

/// <summary>
/// Lightweight metadata about a file on disk, used as a transfer object between the
/// infrastructure layer and application services.
/// </summary>
/// <param name="Length">The file size in bytes.</param>
/// <param name="LastWriteTimeUtc">The last modification timestamp in UTC.</param>
public sealed record FileMetadata(long Length, DateTime LastWriteTimeUtc);

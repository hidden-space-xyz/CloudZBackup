namespace CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Represents metadata for a single file identified by its relative path within a directory tree.
/// </summary>
/// <param name="Path">The relative path of the file.</param>
/// <param name="Length">The file size in bytes.</param>
/// <param name="LastWriteTimeUtc">The last write time of the file in UTC.</param>
public sealed record FileEntry(RelativePath Path, long Length, DateTime LastWriteTimeUtc);

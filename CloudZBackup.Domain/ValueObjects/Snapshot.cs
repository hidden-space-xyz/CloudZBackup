namespace CloudZBackup.Domain.ValueObjects;

/// <summary>
/// An immutable point-in-time snapshot of a directory tree, capturing all files and subdirectories.
/// </summary>
/// <param name="Files">A dictionary mapping each relative file path to its <see cref="FileEntry"/> metadata.</param>
/// <param name="Directories">A set of all relative directory paths found in the tree.</param>
public sealed record Snapshot(
    IReadOnlyDictionary<RelativePath, FileEntry> Files,
    IReadOnlySet<RelativePath> Directories);

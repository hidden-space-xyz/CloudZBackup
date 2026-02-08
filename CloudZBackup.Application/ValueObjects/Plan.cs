namespace CloudZBackup.Application.ValueObjects;

using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Describes the set of file-system operations that a backup must perform,
/// computed by comparing source and destination snapshots.
/// </summary>
/// <param name="DirectoriesToCreate">Directories that exist in the source but not in the destination.</param>
/// <param name="MissingFiles">Files present in the source but absent from the destination.</param>
/// <param name="CommonFiles">Files that exist in both source and destination (candidates for overwrite detection).</param>
/// <param name="ExtraFiles">Files present in the destination but not in the source.</param>
/// <param name="TopLevelExtraDirectories">Top-level directories in the destination that have no corresponding source directory.</param>
public sealed record Plan(
    IReadOnlyList<RelativePath> DirectoriesToCreate,
    IReadOnlyList<RelativePath> MissingFiles,
    IReadOnlyList<RelativePath> CommonFiles,
    IReadOnlyList<RelativePath> ExtraFiles,
    IReadOnlyList<RelativePath> TopLevelExtraDirectories);

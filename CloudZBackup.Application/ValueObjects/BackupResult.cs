namespace CloudZBackup.Application.ValueObjects;

/// <summary>
/// Summarizes the outcome of a completed backup operation.
/// </summary>
/// <param name="DirectoriesCreated">Number of directories created in the destination.</param>
/// <param name="FilesCopied">Number of new files copied to the destination.</param>
/// <param name="FilesOverwritten">Number of existing files overwritten with updated content.</param>
/// <param name="FilesDeleted">Number of extra files deleted from the destination.</param>
/// <param name="DirectoriesDeleted">Number of extra directories deleted from the destination.</param>
public sealed record BackupResult(
    int DirectoriesCreated,
    int FilesCopied,
    int FilesOverwritten,
    int FilesDeleted,
    int DirectoriesDeleted
);

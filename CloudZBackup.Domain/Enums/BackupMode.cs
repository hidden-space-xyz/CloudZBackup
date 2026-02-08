namespace CloudZBackup.Domain.Enums;

/// <summary>
/// Defines the supported backup operation modes.
/// </summary>
public enum BackupMode
{
    /// <summary>
    /// Synchronizes the destination with the source by copying new and changed files
    /// and removing files that no longer exist in the source.
    /// </summary>
    Sync = 0,

    /// <summary>
    /// Copies new files from the source to the destination without modifying
    /// or removing existing destination files.
    /// </summary>
    Add = 1,

    /// <summary>
    /// Removes files from the destination that do not exist in the source.
    /// No files are copied.
    /// </summary>
    Remove = 2,
}

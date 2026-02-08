namespace CloudZBackup.Application.ValueObjects;

/// <summary>
/// Reports incremental progress of a backup operation.
/// </summary>
/// <param name="Phase">A human-readable label describing the current operation phase.</param>
/// <param name="ProcessedItems">The number of items processed so far.</param>
/// <param name="TotalItems">The total number of items expected for the entire operation.</param>
public readonly record struct BackupProgress(string Phase, int ProcessedItems, int TotalItems);

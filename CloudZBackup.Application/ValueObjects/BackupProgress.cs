namespace CloudZBackup.Application.ValueObjects;

public readonly record struct BackupProgress(
    string Phase,
    int ProcessedItems,
    int TotalItems
);

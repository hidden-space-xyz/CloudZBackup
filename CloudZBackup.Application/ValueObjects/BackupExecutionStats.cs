namespace CloudZBackup.Application.ValueObjects;

public readonly record struct BackupExecutionStats(
    int CreatedDirs,
    int Copied,
    int Overwritten,
    int DeletedFiles,
    int DeletedDirs
);

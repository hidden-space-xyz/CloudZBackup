namespace CloudZBackup.Application.UseCases.Result;

public sealed record BackupResult(
    int DirectoriesCreated,
    int FilesCopied,
    int FilesOverwritten,
    int FilesDeleted,
    int DirectoriesDeleted
);

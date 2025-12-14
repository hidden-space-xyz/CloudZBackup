using CloudZBackup.Domain.Enums;

namespace CloudZBackup.Application.UseCases.Request;

public sealed record BackupRequest(string SourcePath, string DestinationPath, BackupMode Mode);

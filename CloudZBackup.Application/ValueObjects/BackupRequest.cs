using CloudZBackup.Domain.Enums;

namespace CloudZBackup.Application.ValueObjects;

public sealed record BackupRequest(string SourcePath, string DestinationPath, BackupMode Mode);

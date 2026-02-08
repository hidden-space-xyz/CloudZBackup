using CloudZBackup.Domain.Enums;

namespace CloudZBackup.Application.ValueObjects;

/// <summary>
/// Encapsulates the parameters required to initiate a backup operation.
/// </summary>
/// <param name="SourcePath">The absolute path to the source directory.</param>
/// <param name="DestinationPath">The absolute path to the destination directory.</param>
/// <param name="Mode">The <see cref="BackupMode"/> that controls which operations are performed.</param>
public sealed record BackupRequest(string SourcePath, string DestinationPath, BackupMode Mode);

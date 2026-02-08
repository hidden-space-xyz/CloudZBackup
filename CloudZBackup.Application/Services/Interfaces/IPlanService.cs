using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services.Interfaces;

/// <summary>
/// Compares source and destination snapshots to build a <see cref="Plan"/> of required operations.
/// </summary>
public interface IPlanService
{
    /// <summary>
    /// Builds a backup plan by comparing the source and destination snapshots according to the specified mode.
    /// </summary>
    /// <param name="mode">The backup mode that determines which operations are included in the plan.</param>
    /// <param name="source">The snapshot of the source directory.</param>
    /// <param name="dest">The snapshot of the destination directory.</param>
    /// <returns>A <see cref="Plan"/> describing all operations to perform.</returns>
    Plan BuildPlan(BackupMode mode, Snapshot source, Snapshot dest);
}

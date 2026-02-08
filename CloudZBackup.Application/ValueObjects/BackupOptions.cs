namespace CloudZBackup.Application.ValueObjects;

/// <summary>
/// Configuration options that control concurrency limits for backup operations.
/// </summary>
public sealed class BackupOptions
{
    /// <summary>
    /// Gets or sets the maximum degree of parallelism for SHA-256 hash computations.
    /// Hashing is both CPU-intensive and IO-intensive.
    /// </summary>
    public int MaxHashConcurrency { get; set; } = Math.Clamp(Environment.ProcessorCount, 2, 16);

    /// <summary>
    /// Gets or sets the maximum degree of parallelism for file copy and delete operations (IO-bound).
    /// </summary>
    public int MaxFileIoConcurrency { get; set; } = 4;
}

namespace CloudZBackup.Application.UseCases.Options;

public sealed class BackupOptions
{
    /// <summary>
    /// Maximum parallelism for SHA-256 computations. Hashing is CPU + IO intensive.
    /// </summary>
    public int MaxHashConcurrency { get; set; } = Math.Clamp(Environment.ProcessorCount, 2, 16);

    /// <summary>
    /// Maximum parallelism for file copy/delete operations (IO bound).
    /// </summary>
    public int MaxFileIoConcurrency { get; set; } = 4;
}

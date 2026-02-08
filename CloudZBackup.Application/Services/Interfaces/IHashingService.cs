namespace CloudZBackup.Application.Services.Interfaces;

/// <summary>
/// Defines a contract for computing cryptographic hashes of files.
/// </summary>
public interface IHashingService
{
    /// <summary>
    /// Computes the SHA-256 hash of the file at the specified path.
    /// </summary>
    /// <param name="filePath">The absolute path to the file to hash.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A byte array containing the SHA-256 hash.</returns>
    Task<byte[]> ComputeSha256Async(string filePath, CancellationToken cancellationToken);
}

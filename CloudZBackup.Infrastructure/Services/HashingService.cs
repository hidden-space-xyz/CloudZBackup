using CloudZBackup.Application.Services.Interfaces;
using System.Security.Cryptography;

namespace CloudZBackup.Infrastructure.Services;

/// <summary>
/// Computes SHA-256 hashes of files using buffered, asynchronous stream reads.
/// </summary>
public sealed class HashingService : IHashingService
{
    /// <inheritdoc />
    public async Task<byte[]> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.SequentialScan
        );

        using var sha = SHA256.Create();

        return await sha.ComputeHashAsync(stream, cancellationToken);
    }
}

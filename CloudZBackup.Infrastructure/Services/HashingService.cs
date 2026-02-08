namespace CloudZBackup.Infrastructure.Services;

using System.Security.Cryptography;
using CloudZBackup.Application.Services.Interfaces;

/// <summary>
/// Computes SHA-256 hashes of files using buffered, asynchronous stream reads.
/// </summary>
public sealed class HashingService : IHashingService
{
    /// <inheritdoc />
    public async Task<byte[]> ComputeSha256Async(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.SequentialScan | FileOptions.Asynchronous);

        return await SHA256.HashDataAsync(stream, cancellationToken);
    }
}

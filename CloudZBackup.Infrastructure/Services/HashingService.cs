using System.Security.Cryptography;
using CloudZBackup.Application.Services.Interfaces;

namespace CloudZBackup.Infrastructure.Services;

public sealed class HashingService : IHashingService
{
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

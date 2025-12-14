using System.Security.Cryptography;
using CloudZBackup.Application.Abstractions.Hashing;

namespace CloudZBackup.Infrastructure.Hashing;

public sealed class Sha256HashCalculator : IHashCalculator
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

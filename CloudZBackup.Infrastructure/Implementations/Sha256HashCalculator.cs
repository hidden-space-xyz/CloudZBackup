using System.Security.Cryptography;
using CloudZBackup.Application.Abstractions;

namespace CloudZBackup.Infrastructure.Implementations;

public sealed class Sha256HashCalculator : IHashCalculator
{
    public async Task<string> ComputeSha256HexAsync(
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
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);

        return Convert.ToHexString(hash); // Uppercase hex, stable format.
    }
}

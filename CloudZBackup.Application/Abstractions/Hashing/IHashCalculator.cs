namespace CloudZBackup.Application.Abstractions.Hashing;

public interface IHashCalculator
{
    Task<byte[]> ComputeSha256Async(string filePath, CancellationToken cancellationToken);
}

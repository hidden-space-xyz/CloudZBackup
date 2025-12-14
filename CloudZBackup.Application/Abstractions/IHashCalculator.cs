namespace CloudZBackup.Application.Abstractions;

public interface IHashCalculator
{
    Task<string> ComputeSha256HexAsync(string filePath, CancellationToken cancellationToken);
}

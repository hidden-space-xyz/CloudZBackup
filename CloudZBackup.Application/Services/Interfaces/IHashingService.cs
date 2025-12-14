namespace CloudZBackup.Application.Services.Interfaces;

public interface IHashingService
{
    Task<byte[]> ComputeSha256Async(string filePath, CancellationToken cancellationToken);
}

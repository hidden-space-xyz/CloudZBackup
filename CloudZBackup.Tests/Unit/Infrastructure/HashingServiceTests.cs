using CloudZBackup.Infrastructure.Services;

namespace CloudZBackup.Tests.Unit.Infrastructure;

[TestFixture]
public sealed class HashingServiceTests
{
    private HashingService _sut = null!;
    private string _testRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new HashingService();
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "CloudZBackupHashTests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Test]
    public async Task ComputeSha256Async_ReturnsConsistentHash()
    {
        string file = Path.Combine(_testRoot, "hash_test.txt");
        File.WriteAllText(file, "deterministic content");

        byte[] hash1 = await _sut.ComputeSha256Async(file, CancellationToken.None);
        byte[] hash2 = await _sut.ComputeSha256Async(file, CancellationToken.None);

        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public async Task ComputeSha256Async_Returns32Bytes()
    {
        string file = Path.Combine(_testRoot, "size_test.txt");
        File.WriteAllText(file, "test");

        byte[] hash = await _sut.ComputeSha256Async(file, CancellationToken.None);

        Assert.That(hash, Has.Length.EqualTo(32));
    }

    [Test]
    public async Task ComputeSha256Async_DifferentContent_DifferentHash()
    {
        string file1 = Path.Combine(_testRoot, "file1.txt");
        string file2 = Path.Combine(_testRoot, "file2.txt");
        File.WriteAllText(file1, "content A");
        File.WriteAllText(file2, "content B");

        byte[] hash1 = await _sut.ComputeSha256Async(file1, CancellationToken.None);
        byte[] hash2 = await _sut.ComputeSha256Async(file2, CancellationToken.None);

        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    [Test]
    public async Task ComputeSha256Async_EmptyFile_ReturnsValidHash()
    {
        string file = Path.Combine(_testRoot, "empty.txt");
        File.WriteAllText(file, "");

        byte[] hash = await _sut.ComputeSha256Async(file, CancellationToken.None);

        Assert.That(hash, Has.Length.EqualTo(32));
    }
}

namespace CloudZBackup.Tests.Unit.Infrastructure;

using CloudZBackup.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="HashingService"/>.
/// </summary>
[TestFixture]
public sealed class HashingServiceTests
{
    private HashingService sut = null!;
    private string testRoot = null!;

    /// <summary>
    /// Verifies that files with different content produce different SHA-256 hashes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ComputeSha256AsyncDifferentContentDifferentHash()
    {
        string file1 = Path.Combine(this.testRoot, "file1.txt");
        string file2 = Path.Combine(this.testRoot, "file2.txt");
        await File.WriteAllTextAsync(file1, "content A");
        await File.WriteAllTextAsync(file2, "content B");

        byte[] hash1 = await this.sut.ComputeSha256Async(file1, CancellationToken.None);
        byte[] hash2 = await this.sut.ComputeSha256Async(file2, CancellationToken.None);

        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    /// <summary>
    /// Verifies that computing the SHA-256 hash of an empty file returns a valid 32-byte hash.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ComputeSha256AsyncEmptyFileReturnsValidHash()
    {
        string file = Path.Combine(this.testRoot, "empty.txt");
        await File.WriteAllTextAsync(file, string.Empty);

        byte[] hash = await this.sut.ComputeSha256Async(file, CancellationToken.None);

        Assert.That(hash, Has.Length.EqualTo(32));
    }

    /// <summary>
    /// Verifies that the SHA-256 hash always returns exactly 32 bytes.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ComputeSha256AsyncReturns32Bytes()
    {
        string file = Path.Combine(this.testRoot, "size_test.txt");
        await File.WriteAllTextAsync(file, "test");

        byte[] hash = await this.sut.ComputeSha256Async(file, CancellationToken.None);

        Assert.That(hash, Has.Length.EqualTo(32));
    }

    /// <summary>
    /// Verifies that computing the SHA-256 hash of the same file twice returns identical results.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ComputeSha256AsyncReturnsConsistentHash()
    {
        string file = Path.Combine(this.testRoot, "hash_test.txt");
        await File.WriteAllTextAsync(file, "deterministic content");

        byte[] hash1 = await this.sut.ComputeSha256Async(file, CancellationToken.None);
        byte[] hash2 = await this.sut.ComputeSha256Async(file, CancellationToken.None);

        Assert.That(hash1, Is.EqualTo(hash2));
    }

    /// <summary>
    /// Initializes the service under test and creates a unique temporary directory.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.sut = new HashingService();
        this.testRoot = Path.Combine(
            Path.GetTempPath(),
            "CloudZBackupHashTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.testRoot);
    }

    /// <summary>
    /// Cleans up the temporary directory after each test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this.testRoot))
        {
            Directory.Delete(this.testRoot, recursive: true);
        }
    }
}

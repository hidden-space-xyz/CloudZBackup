namespace CloudZBackup.Tests.Unit.Application;

using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using NSubstitute;

/// <summary>
/// Unit tests for <see cref="OverwriteDetectionService"/>.
/// </summary>
[TestFixture]
public sealed class OverwriteDetectionServiceTests
{
    private static readonly DateTime BaseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private IFileSystemService fileSystem = null!;
    private IHashingService hashingService = null!;
    private OverwriteDetectionService sut = null!;

    /// <summary>
    /// Verifies that a file with a different size is marked for overwrite without
    /// performing a hash comparison.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DifferentFileSizeMarkedForOverwriteWithoutHashing()
    {
        var rp = new RelativePath("file.txt");
        var commonFiles = new List<RelativePath> { rp };
        var sourceFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };
        var destFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 200, BaseTime) };

        List<RelativePath> result = await this.sut.ComputeFilesToOverwriteAsync(
            commonFiles,
            sourceFiles,
            destFiles,
            "/src",
            "/dst",
            CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
        await this.hashingService
            .DidNotReceive()
            .ComputeSha256Async(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that an empty common-files list produces an empty overwrite list.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EmptyCommonFilesReturnsEmptyList()
    {
        List<RelativePath> result = await this.sut.ComputeFilesToOverwriteAsync(
            [],
            new Dictionary<RelativePath, FileEntry>(),
            new Dictionary<RelativePath, FileEntry>(),
            "/src",
            "/dst",
            CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Verifies that a file with the same size and timestamp is not marked for overwrite.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SameSizeAndTimestampNotMarkedForOverwrite()
    {
        var rp = new RelativePath("file.txt");
        var commonFiles = new List<RelativePath> { rp };
        var sourceFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };
        var destFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };

        List<RelativePath> result = await this.sut.ComputeFilesToOverwriteAsync(
            commonFiles,
            sourceFiles,
            destFiles,
            "/src",
            "/dst",
            CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Verifies that a file with the same size but different timestamp and different hash
    /// is marked for overwrite.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SameSizeDifferentTimestampDifferentHashMarkedForOverwrite()
    {
        var rp = new RelativePath("file.txt");
        var commonFiles = new List<RelativePath> { rp };
        var sourceFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };
        var destFiles = new Dictionary<RelativePath, FileEntry>
        {
            [rp] = new(rp, 100, BaseTime.AddHours(1)),
        };

        this.hashingService
            .ComputeSha256Async("/src/file.txt", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1, 2, 3 });
        this.hashingService
            .ComputeSha256Async("/dst/file.txt", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 4, 5, 6 });

        List<RelativePath> result = await this.sut.ComputeFilesToOverwriteAsync(
            commonFiles,
            sourceFiles,
            destFiles,
            "/src",
            "/dst",
            CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    /// <summary>
    /// Verifies that a file with the same size but different timestamp and same hash
    /// is not marked for overwrite.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SameSizeDifferentTimestampSameHashNotMarkedForOverwrite()
    {
        var rp = new RelativePath("file.txt");
        var commonFiles = new List<RelativePath> { rp };
        var sourceFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };
        var destFiles = new Dictionary<RelativePath, FileEntry>
        {
            [rp] = new(rp, 100, BaseTime.AddHours(1)),
        };

        byte[] hash = [1, 2, 3, 4, 5, 6, 7, 8];
        this.hashingService
            .ComputeSha256Async(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(hash);

        List<RelativePath> result = await this.sut.ComputeFilesToOverwriteAsync(
            commonFiles,
            sourceFiles,
            destFiles,
            "/src",
            "/dst",
            CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// Initializes test dependencies before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.hashingService = Substitute.For<IHashingService>();
        this.fileSystem = Substitute.For<IFileSystemService>();
        var options = Options.Create(new BackupOptions { MaxHashConcurrency = 1 });
        this.sut = new OverwriteDetectionService(this.hashingService, options, this.fileSystem);

        this.fileSystem
            .Combine(Arg.Any<string>(), Arg.Any<RelativePath>())
            .Returns(ci => $"{ci.ArgAt<string>(0)}/{ci.ArgAt<RelativePath>(1).Value}");
    }
}

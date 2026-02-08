using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CloudZBackup.Tests.Unit.Application;

[TestFixture]
public sealed class OverwriteDetectionServiceTests
{
    private IHashingService _hashingService = null!;
    private IFileSystemService _fileSystem = null!;
    private OverwriteDetectionService _sut = null!;

    private static readonly DateTime BaseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _hashingService = Substitute.For<IHashingService>();
        _fileSystem = Substitute.For<IFileSystemService>();
        var options = Options.Create(new BackupOptions { MaxHashConcurrency = 1 });
        _sut = new OverwriteDetectionService(_hashingService, options, _fileSystem);

        _fileSystem
            .Combine(Arg.Any<string>(), Arg.Any<RelativePath>())
            .Returns(ci => $"{ci.ArgAt<string>(0)}/{ci.ArgAt<RelativePath>(1).Value}");
    }

    [Test]
    public async Task DifferentFileSize_MarkedForOverwrite_WithoutHashing()
    {
        var rp = new RelativePath("file.txt");
        var commonFiles = new List<RelativePath> { rp };
        var sourceFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };
        var destFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 200, BaseTime) };

        List<RelativePath> result = await _sut.ComputeFilesToOverwriteAsync(
            commonFiles,
            sourceFiles,
            destFiles,
            "/src",
            "/dst",
            CancellationToken.None
        );

        Assert.That(result, Has.Count.EqualTo(1));
        await _hashingService
            .DidNotReceive()
            .ComputeSha256Async(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SameSizeAndTimestamp_NotMarkedForOverwrite()
    {
        var rp = new RelativePath("file.txt");
        var commonFiles = new List<RelativePath> { rp };
        var sourceFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };
        var destFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };

        List<RelativePath> result = await _sut.ComputeFilesToOverwriteAsync(
            commonFiles,
            sourceFiles,
            destFiles,
            "/src",
            "/dst",
            CancellationToken.None
        );

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SameSize_DifferentTimestamp_DifferentHash_MarkedForOverwrite()
    {
        var rp = new RelativePath("file.txt");
        var commonFiles = new List<RelativePath> { rp };
        var sourceFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };
        var destFiles = new Dictionary<RelativePath, FileEntry>
        {
            [rp] = new(rp, 100, BaseTime.AddHours(1)),
        };

        _hashingService
            .ComputeSha256Async("/src/file.txt", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1, 2, 3 });
        _hashingService
            .ComputeSha256Async("/dst/file.txt", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 4, 5, 6 });

        List<RelativePath> result = await _sut.ComputeFilesToOverwriteAsync(
            commonFiles,
            sourceFiles,
            destFiles,
            "/src",
            "/dst",
            CancellationToken.None
        );

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SameSize_DifferentTimestamp_SameHash_NotMarkedForOverwrite()
    {
        var rp = new RelativePath("file.txt");
        var commonFiles = new List<RelativePath> { rp };
        var sourceFiles = new Dictionary<RelativePath, FileEntry> { [rp] = new(rp, 100, BaseTime) };
        var destFiles = new Dictionary<RelativePath, FileEntry>
        {
            [rp] = new(rp, 100, BaseTime.AddHours(1)),
        };

        byte[] hash = [1, 2, 3, 4, 5, 6, 7, 8];
        _hashingService
            .ComputeSha256Async(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(hash);

        List<RelativePath> result = await _sut.ComputeFilesToOverwriteAsync(
            commonFiles,
            sourceFiles,
            destFiles,
            "/src",
            "/dst",
            CancellationToken.None
        );

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task EmptyCommonFiles_ReturnsEmptyList()
    {
        List<RelativePath> result = await _sut.ComputeFilesToOverwriteAsync(
            [],
            new Dictionary<RelativePath, FileEntry>(),
            new Dictionary<RelativePath, FileEntry>(),
            "/src",
            "/dst",
            CancellationToken.None
        );

        Assert.That(result, Is.Empty);
    }
}

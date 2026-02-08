using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.ValueObjects;
using NSubstitute;

namespace CloudZBackup.Tests.Unit.Application;

[TestFixture]
public sealed class SnapshotServiceTests
{
    private IFileSystemService _fileSystem = null!;
    private SnapshotService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _fileSystem = Substitute.For<IFileSystemService>();
        _sut = new SnapshotService(_fileSystem);
    }

    [Test]
    public void CaptureSnapshot_WithFilesAndDirectories_ReturnsPopulatedSnapshot()
    {
        string root = "/source";

        _fileSystem.EnumerateDirectoriesRecursive(root).Returns(["/source/subdir"]);
        _fileSystem
            .EnumerateFilesRecursive(root)
            .Returns(["/source/file1.txt", "/source/subdir/file2.txt"]);
        _fileSystem
            .GetFileMetadata("/source/file1.txt")
            .Returns(new FileMetadata(100, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        _fileSystem
            .GetFileMetadata("/source/subdir/file2.txt")
            .Returns(new FileMetadata(200, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        Snapshot result = _sut.CaptureSnapshot(
            root,
            includeFileMetadata: true,
            CancellationToken.None
        );

        Assert.Multiple(() =>
        {
            Assert.That(result.Files, Has.Count.EqualTo(2));
            Assert.That(result.Directories, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void CaptureSnapshot_WithoutMetadata_SetsZeroLengthAndDefaultTime()
    {
        string root = "/source";

        _fileSystem.EnumerateDirectoriesRecursive(root).Returns([]);
        _fileSystem.EnumerateFilesRecursive(root).Returns(["/source/file.txt"]);

        Snapshot result = _sut.CaptureSnapshot(
            root,
            includeFileMetadata: false,
            CancellationToken.None
        );

        FileEntry entry = result.Files.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Length, Is.EqualTo(0));
            Assert.That(entry.LastWriteTimeUtc, Is.EqualTo(default(DateTime)));
        });
    }

    [Test]
    public void CaptureSnapshot_EmptyDirectory_ReturnsEmptySnapshot()
    {
        string root = "/empty";
        _fileSystem.EnumerateDirectoriesRecursive(root).Returns([]);
        _fileSystem.EnumerateFilesRecursive(root).Returns([]);

        Snapshot result = _sut.CaptureSnapshot(
            root,
            includeFileMetadata: true,
            CancellationToken.None
        );

        Assert.Multiple(() =>
        {
            Assert.That(result.Files, Is.Empty);
            Assert.That(result.Directories, Is.Empty);
        });
    }

    [Test]
    public void CaptureSnapshot_Cancellation_ThrowsOperationCanceledException()
    {
        string root = "/source";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The cancellation check happens every 256 items, so we provide enough items
        var dirs = Enumerable.Range(0, 300).Select(i => $"/source/dir{i}").ToList();
        _fileSystem.EnumerateDirectoriesRecursive(root).Returns(dirs);

        Assert.Throws<OperationCanceledException>(() =>
            _sut.CaptureSnapshot(root, true, cts.Token)
        );
    }

    [Test]
    public void CreateEmptySnapshot_ReturnsSnapshotWithNoFilesOrDirectories()
    {
        Snapshot result = _sut.CreateEmptySnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(result.Files, Is.Empty);
            Assert.That(result.Directories, Is.Empty);
        });
    }
}

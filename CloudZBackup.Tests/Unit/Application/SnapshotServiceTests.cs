namespace CloudZBackup.Tests.Unit.Application;

using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.ValueObjects;
using NSubstitute;

/// <summary>
/// Unit tests for <see cref="SnapshotService"/>.
/// </summary>
[TestFixture]
public sealed class SnapshotServiceTests
{
    private IFileSystemService fileSystem = null!;
    private SnapshotService sut = null!;

    /// <summary>
    /// Verifies that <see cref="SnapshotService.CaptureSnapshot"/> throws
    /// <see cref="OperationCanceledException"/> when the cancellation token is already cancelled.
    /// </summary>
    [Test]
    public void CaptureSnapshotCancellationThrowsOperationCanceledException()
    {
        string root = "/source";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The cancellation check happens every 256 items, so we provide enough items
        var dirs = Enumerable.Range(0, 300).Select(i => $"/source/dir{i}").ToList();
        this.fileSystem.EnumerateDirectoriesRecursive(root).Returns(dirs);

        Assert.Throws<OperationCanceledException>(() =>
            this.sut.CaptureSnapshot(root, true, cts.Token));
    }

    /// <summary>
    /// Verifies that capturing a snapshot of an empty directory returns a snapshot
    /// with no files and no directories.
    /// </summary>
    [Test]
    public void CaptureSnapshotEmptyDirectoryReturnsEmptySnapshot()
    {
        string root = "/empty";
        this.fileSystem.EnumerateDirectoriesRecursive(root).Returns([]);
        this.fileSystem.EnumerateFilesRecursive(root).Returns([]);

        Snapshot result = this.sut.CaptureSnapshot(
            root,
            includeFileMetadata: true,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Files, Is.Empty);
            Assert.That(result.Directories, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that capturing a snapshot of a directory with files and subdirectories
    /// returns a populated snapshot containing all discovered entries.
    /// </summary>
    [Test]
    public void CaptureSnapshotWithFilesAndDirectoriesReturnsPopulatedSnapshot()
    {
        string root = "/source";

        this.fileSystem.EnumerateDirectoriesRecursive(root).Returns(["/source/subdir"]);
        this.fileSystem
            .EnumerateFilesRecursive(root)
            .Returns(["/source/file1.txt", "/source/subdir/file2.txt"]);
        this.fileSystem
            .GetFileMetadata("/source/file1.txt")
            .Returns(new FileMetadata(100, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        this.fileSystem
            .GetFileMetadata("/source/subdir/file2.txt")
            .Returns(new FileMetadata(200, new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)));

        Snapshot result = this.sut.CaptureSnapshot(
            root,
            includeFileMetadata: true,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Files, Has.Count.EqualTo(2));
            Assert.That(result.Directories, Has.Count.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies that capturing a snapshot without metadata produces file entries
    /// with zero length and default <see cref="DateTime"/> values.
    /// </summary>
    [Test]
    public void CaptureSnapshotWithoutMetadataSetsZeroLengthAndDefaultTime()
    {
        string root = "/source";

        this.fileSystem.EnumerateDirectoriesRecursive(root).Returns([]);
        this.fileSystem.EnumerateFilesRecursive(root).Returns(["/source/file.txt"]);

        Snapshot result = this.sut.CaptureSnapshot(
            root,
            includeFileMetadata: false,
            CancellationToken.None);

        FileEntry entry = result.Files.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Length, Is.EqualTo(0));
            Assert.That(entry.LastWriteTimeUtc, Is.EqualTo(default(DateTime)));
        });
    }

    /// <summary>
    /// Verifies that <see cref="SnapshotService.CreateEmptySnapshot"/> returns
    /// a snapshot with empty file and directory collections.
    /// </summary>
    [Test]
    public void CreateEmptySnapshotReturnsSnapshotWithNoFilesOrDirectories()
    {
        Snapshot result = this.sut.CreateEmptySnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(result.Files, Is.Empty);
            Assert.That(result.Directories, Is.Empty);
        });
    }

    /// <summary>
    /// Initializes test dependencies before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.fileSystem = Substitute.For<IFileSystemService>();
        this.sut = new SnapshotService(this.fileSystem);
    }
}

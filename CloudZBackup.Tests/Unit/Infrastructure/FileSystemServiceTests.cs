namespace CloudZBackup.Tests.Unit.Infrastructure;

using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using CloudZBackup.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="FileSystemService"/> using real temporary directories.
/// </summary>
[TestFixture]
public sealed class FileSystemServiceTests
{
    private FileSystemService sut = null!;
    private string testRoot = null!;

    /// <summary>
    /// Verifies that <see cref="FileSystemService.Combine"/> produces the correct absolute path.
    /// </summary>
    [Test]
    public void CombineProducesCorrectPath()
    {
        var rel = new RelativePath("sub/file.txt");
        string result = this.sut.Combine(this.testRoot, rel);

        Assert.That(result, Is.EqualTo(Path.Combine(this.testRoot, "sub", "file.txt")));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.CopyFileAsync"/> copies file content correctly.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CopyFileAsyncCopiesContentCorrectly()
    {
        string src = Path.Combine(this.testRoot, "source.txt");
        string dst = Path.Combine(this.testRoot, "dest.txt");
        await File.WriteAllTextAsync(src, "copy this");

        await this.sut.CopyFileAsync(src, dst, overwrite: false, null, CancellationToken.None);

        Assert.That(await File.ReadAllTextAsync(dst), Is.EqualTo("copy this"));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.CopyFileAsync"/> successfully copies a file
    /// when a last-write time is specified.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CopyFileAsyncWithLastWriteTimeCopiesFileSuccessfully()
    {
        string src = Path.Combine(this.testRoot, "source.txt");
        string dst = Path.Combine(this.testRoot, "dest.txt");
        await File.WriteAllTextAsync(src, "timed");

        var expectedTime = new DateTime(2023, 3, 15, 10, 0, 0, DateTimeKind.Utc);

        await this.sut.CopyFileAsync(src, dst, overwrite: false, expectedTime, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(dst), Is.True);
            Assert.That(File.ReadAllText(dst), Is.EqualTo("timed"));
        });
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.CopyFileAsync"/> overwrites an existing file
    /// when the overwrite flag is set.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CopyFileAsyncWithOverwriteOverwritesExistingFile()
    {
        string src = Path.Combine(this.testRoot, "source.txt");
        string dst = Path.Combine(this.testRoot, "dest.txt");
        await File.WriteAllTextAsync(src, "new content");
        await File.WriteAllTextAsync(dst, "old content");

        await this.sut.CopyFileAsync(src, dst, overwrite: true, null, CancellationToken.None);

        Assert.That(await File.ReadAllTextAsync(dst), Is.EqualTo("new content"));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.CreateDirectory"/> creates the specified directory.
    /// </summary>
    [Test]
    public void CreateDirectoryCreatesTheDirectory()
    {
        string dir = Path.Combine(this.testRoot, "newdir");

        this.sut.CreateDirectory(dir);

        Assert.That(Directory.Exists(dir), Is.True);
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.DeleteDirectoryIfExists"/> deletes the directory
    /// and all of its contents recursively.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DeleteDirectoryIfExistsDeletesRecursively()
    {
        string dir = Path.Combine(this.testRoot, "deldir");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "child.txt"), "data");

        this.sut.DeleteDirectoryIfExists(dir, recursive: true);

        Assert.That(Directory.Exists(dir), Is.False);
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.DeleteFileIfExists"/> deletes the specified file.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task DeleteFileIfExistsDeletesFile()
    {
        string file = Path.Combine(this.testRoot, "delete_me.txt");
        await File.WriteAllTextAsync(file, "data");

        this.sut.DeleteFileIfExists(file);

        Assert.That(File.Exists(file), Is.False);
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.DirectoryExists"/> returns <see langword="false"/>
    /// for a non-existing directory.
    /// </summary>
    [Test]
    public void DirectoryExistsReturnsFalseForNonExisting()
    {
        Assert.That(this.sut.DirectoryExists(Path.Combine(this.testRoot, "nope")), Is.False);
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.DirectoryExists"/> returns <see langword="true"/>
    /// for an existing directory.
    /// </summary>
    [Test]
    public void DirectoryExistsReturnsTrueForExisting()
    {
        Assert.That(this.sut.DirectoryExists(this.testRoot), Is.True);
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.EnsureSourceExists"/> does not throw
    /// when the directory exists.
    /// </summary>
    [Test]
    public void EnsureSourceExistsDoesNotThrowForExistingDirectory()
    {
        Assert.DoesNotThrow(() => this.sut.EnsureSourceExists(this.testRoot));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.EnsureSourceExists"/> throws
    /// <see cref="DirectoryNotFoundException"/> for a missing directory.
    /// </summary>
    [Test]
    public void EnsureSourceExistsThrowsForMissingDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            this.sut.EnsureSourceExists(Path.Combine(this.testRoot, "missing")));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.EnumerateDirectoriesRecursive"/>
    /// discovers all nested subdirectories.
    /// </summary>
    [Test]
    public void EnumerateDirectoriesRecursiveFindsAllDirectories()
    {
        string sub = Path.Combine(this.testRoot, "sub");
        string nested = Path.Combine(sub, "nested");
        Directory.CreateDirectory(nested);

        var dirs = this.sut.EnumerateDirectoriesRecursive(this.testRoot).ToList();

        Assert.That(dirs, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.EnumerateFilesRecursive"/>
    /// discovers all files including those in subdirectories.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task EnumerateFilesRecursiveFindsAllFiles()
    {
        await File.WriteAllTextAsync(Path.Combine(this.testRoot, "a.txt"), "a");
        string sub = Path.Combine(this.testRoot, "sub");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "b.txt"), "b");

        var files = this.sut.EnumerateFilesRecursive(this.testRoot).ToList();

        Assert.That(files, Has.Count.EqualTo(2));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.GetFileMetadata"/> returns valid metadata
    /// with a non-zero length and a non-default last-write time.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task GetFileMetadataReturnsCorrectMetadata()
    {
        string file = Path.Combine(this.testRoot, "meta.txt");
        await File.WriteAllTextAsync(file, "hello world");

        FileMetadata meta = this.sut.GetFileMetadata(file);

        Assert.Multiple(() =>
        {
            Assert.That(meta.Length, Is.GreaterThan(0));
            Assert.That(meta.LastWriteTimeUtc, Is.Not.EqualTo(default(DateTime)));
        });
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.PrepareDestination"/> in sync mode creates
    /// the destination directory when it does not exist.
    /// </summary>
    [Test]
    public void PrepareDestinationSyncModeCreatesDestIfNotExists()
    {
        string dest = Path.Combine(this.testRoot, "newdest");

        bool created = this.sut.PrepareDestination(BackupMode.Sync, dest);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(Directory.Exists(dest), Is.True);
        });
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.PrepareDestination"/> in sync mode returns
    /// <see langword="false"/> when the destination directory already exists.
    /// </summary>
    [Test]
    public void PrepareDestinationSyncModeReturnsFalseIfExists()
    {
        bool created = this.sut.PrepareDestination(BackupMode.Sync, this.testRoot);

        Assert.That(created, Is.False);
    }

    /// <summary>
    /// Initializes the service under test and creates a unique temporary directory.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.sut = new FileSystemService();
        this.testRoot = Path.Combine(
            Path.GetTempPath(),
            "CloudZBackupFsTests",
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

    /// <summary>
    /// Verifies that <see cref="FileSystemService.ValidateAndNormalize"/> throws
    /// <see cref="ArgumentException"/> when the destination path is empty.
    /// </summary>
    [Test]
    public void ValidateAndNormalizeThrowsForEmptyDestination()
    {
        var request = new BackupRequest("/source", string.Empty, BackupMode.Sync);

        Assert.Throws<ArgumentException>(() => this.sut.ValidateAndNormalize(request));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.ValidateAndNormalize"/> throws
    /// <see cref="ArgumentException"/> when the source path is empty.
    /// </summary>
    [Test]
    public void ValidateAndNormalizeThrowsForEmptySource()
    {
        var request = new BackupRequest(string.Empty, "/dest", BackupMode.Sync);

        Assert.Throws<ArgumentException>(() => this.sut.ValidateAndNormalize(request));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.ValidateNoOverlap"/> does not throw
    /// when source and destination are sibling directories.
    /// </summary>
    [Test]
    public void ValidateNoOverlapDoesNotThrowForSiblingDirectories()
    {
        string source = Path.Combine(this.testRoot, "dirA");
        string dest = Path.Combine(this.testRoot, "dirB");

        Assert.DoesNotThrow(() => this.sut.ValidateNoOverlap(source, dest));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.ValidateNoOverlap"/> throws
    /// <see cref="InvalidOperationException"/> when the destination is inside the source.
    /// </summary>
    [Test]
    public void ValidateNoOverlapThrowsWhenDestInsideSource()
    {
        string source = Path.Combine(this.testRoot, "parent");
        string dest = Path.Combine(source, "child");

        Assert.Throws<InvalidOperationException>(() => this.sut.ValidateNoOverlap(source, dest));
    }

    /// <summary>
    /// Verifies that <see cref="FileSystemService.ValidateNoOverlap"/> throws
    /// <see cref="InvalidOperationException"/> when the source is inside the destination.
    /// </summary>
    [Test]
    public void ValidateNoOverlapThrowsWhenSourceInsideDest()
    {
        string dest = Path.Combine(this.testRoot, "parent");
        string source = Path.Combine(dest, "child");

        Assert.Throws<InvalidOperationException>(() => this.sut.ValidateNoOverlap(source, dest));
    }
}

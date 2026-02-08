using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using CloudZBackup.Infrastructure.Services;

namespace CloudZBackup.Tests.Unit.Infrastructure;

[TestFixture]
public sealed class FileSystemServiceTests
{
    private FileSystemService _sut = null!;
    private string _testRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new FileSystemService();
        _testRoot = Path.Combine(Path.GetTempPath(), "CloudZBackupFsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Test]
    public void Combine_ProducesCorrectPath()
    {
        var rel = new RelativePath("sub/file.txt");
        string result = _sut.Combine(_testRoot, rel);

        Assert.That(result, Is.EqualTo(Path.Combine(_testRoot, "sub", "file.txt")));
    }

    [Test]
    public void CreateDirectory_CreatesTheDirectory()
    {
        string dir = Path.Combine(_testRoot, "newdir");

        _sut.CreateDirectory(dir);

        Assert.That(Directory.Exists(dir), Is.True);
    }

    [Test]
    public void DirectoryExists_ReturnsTrueForExisting()
    {
        Assert.That(_sut.DirectoryExists(_testRoot), Is.True);
    }

    [Test]
    public void DirectoryExists_ReturnsFalseForNonExisting()
    {
        Assert.That(_sut.DirectoryExists(Path.Combine(_testRoot, "nope")), Is.False);
    }

    [Test]
    public void DeleteFileIfExists_DeletesFile()
    {
        string file = Path.Combine(_testRoot, "delete_me.txt");
        File.WriteAllText(file, "data");

        _sut.DeleteFileIfExists(file);

        Assert.That(File.Exists(file), Is.False);
    }

    [Test]
    public void DeleteDirectoryIfExists_DeletesRecursively()
    {
        string dir = Path.Combine(_testRoot, "deldir");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "child.txt"), "data");

        _sut.DeleteDirectoryIfExists(dir, recursive: true);

        Assert.That(Directory.Exists(dir), Is.False);
    }

    [Test]
    public void EnsureSourceExists_ThrowsForMissingDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => _sut.EnsureSourceExists(Path.Combine(_testRoot, "missing")));
    }

    [Test]
    public void EnsureSourceExists_DoesNotThrowForExistingDirectory()
    {
        Assert.DoesNotThrow(() => _sut.EnsureSourceExists(_testRoot));
    }

    [Test]
    public void GetFileMetadata_ReturnsCorrectMetadata()
    {
        string file = Path.Combine(_testRoot, "meta.txt");
        File.WriteAllText(file, "hello world");

        FileMetadata meta = _sut.GetFileMetadata(file);

        Assert.Multiple(() =>
        {
            Assert.That(meta.Length, Is.GreaterThan(0));
            Assert.That(meta.LastWriteTimeUtc, Is.Not.EqualTo(default(DateTime)));
        });
    }

    [Test]
    public void EnumerateFilesRecursive_FindsAllFiles()
    {
        File.WriteAllText(Path.Combine(_testRoot, "a.txt"), "a");
        string sub = Path.Combine(_testRoot, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "b.txt"), "b");

        var files = _sut.EnumerateFilesRecursive(_testRoot).ToList();

        Assert.That(files, Has.Count.EqualTo(2));
    }

    [Test]
    public void EnumerateDirectoriesRecursive_FindsAllDirectories()
    {
        string sub = Path.Combine(_testRoot, "sub");
        string nested = Path.Combine(sub, "nested");
        Directory.CreateDirectory(nested);

        var dirs = _sut.EnumerateDirectoriesRecursive(_testRoot).ToList();

        Assert.That(dirs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CopyFileAsync_CopiesContentCorrectly()
    {
        string src = Path.Combine(_testRoot, "source.txt");
        string dst = Path.Combine(_testRoot, "dest.txt");
        File.WriteAllText(src, "copy this");

        await _sut.CopyFileAsync(src, dst, overwrite: false, null, CancellationToken.None);

        Assert.That(File.ReadAllText(dst), Is.EqualTo("copy this"));
    }

    [Test]
    public async Task CopyFileAsync_WithOverwrite_OverwritesExistingFile()
    {
        string src = Path.Combine(_testRoot, "source.txt");
        string dst = Path.Combine(_testRoot, "dest.txt");
        File.WriteAllText(src, "new content");
        File.WriteAllText(dst, "old content");

        await _sut.CopyFileAsync(src, dst, overwrite: true, null, CancellationToken.None);

        Assert.That(File.ReadAllText(dst), Is.EqualTo("new content"));
    }

    [Test]
    public async Task CopyFileAsync_WithLastWriteTime_CopiesFileSuccessfully()
    {
        string src = Path.Combine(_testRoot, "source.txt");
        string dst = Path.Combine(_testRoot, "dest.txt");
        File.WriteAllText(src, "timed");

        var expectedTime = new DateTime(2023, 3, 15, 10, 0, 0, DateTimeKind.Utc);

        await _sut.CopyFileAsync(src, dst, overwrite: false, expectedTime, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(dst), Is.True);
            Assert.That(File.ReadAllText(dst), Is.EqualTo("timed"));
        });
    }

    [Test]
    public void ValidateAndNormalize_ThrowsForEmptySource()
    {
        var request = new BackupRequest("", "/dest", BackupMode.Sync);

        Assert.Throws<ArgumentException>(() => _sut.ValidateAndNormalize(request));
    }

    [Test]
    public void ValidateAndNormalize_ThrowsForEmptyDestination()
    {
        var request = new BackupRequest("/source", "", BackupMode.Sync);

        Assert.Throws<ArgumentException>(() => _sut.ValidateAndNormalize(request));
    }

    [Test]
    public void ValidateNoOverlap_ThrowsWhenDestInsideSource()
    {
        string source = Path.Combine(_testRoot, "parent");
        string dest = Path.Combine(source, "child");

        Assert.Throws<InvalidOperationException>(
            () => _sut.ValidateNoOverlap(source, dest));
    }

    [Test]
    public void ValidateNoOverlap_ThrowsWhenSourceInsideDest()
    {
        string dest = Path.Combine(_testRoot, "parent");
        string source = Path.Combine(dest, "child");

        Assert.Throws<InvalidOperationException>(
            () => _sut.ValidateNoOverlap(source, dest));
    }

    [Test]
    public void ValidateNoOverlap_DoesNotThrowForSiblingDirectories()
    {
        string source = Path.Combine(_testRoot, "dirA");
        string dest = Path.Combine(_testRoot, "dirB");

        Assert.DoesNotThrow(() => _sut.ValidateNoOverlap(source, dest));
    }

    [Test]
    public void PrepareDestination_SyncMode_CreatesDestIfNotExists()
    {
        string dest = Path.Combine(_testRoot, "newdest");

        bool created = _sut.PrepareDestination(BackupMode.Sync, dest);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(Directory.Exists(dest), Is.True);
        });
    }

    [Test]
    public void PrepareDestination_SyncMode_ReturnsFalseIfExists()
    {
        bool created = _sut.PrepareDestination(BackupMode.Sync, _testRoot);

        Assert.That(created, Is.False);
    }
}

using CloudZBackup.Application.Orchestrators;
using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CloudZBackup.Tests.Integration;

/// <summary>
/// End-to-end integration tests that exercise the full backup pipeline
/// using real file system operations on temporary directories.
/// </summary>
[TestFixture]
public sealed class BackupIntegrationTests
{
    private string _testRoot = null!;
    private string _sourceDir = null!;
    private string _destDir = null!;

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "CloudZBackupTests", Guid.NewGuid().ToString("N"));
        _sourceDir = Path.Combine(_testRoot, "source");
        _destDir = Path.Combine(_testRoot, "dest");
        Directory.CreateDirectory(_sourceDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    private static BackupOrchestrator CreateOrchestrator()
    {
        IFileSystemService fileSystem = new FileSystemService();
        IHashingService hashing = new HashingService();
        var options = Options.Create(new BackupOptions { MaxHashConcurrency = 1, MaxFileIoConcurrency = 1 });

        var snapshotService = new SnapshotService(fileSystem);
        var planService = new PlanService();
        var overwriteDetection = new OverwriteDetectionService(hashing, options, fileSystem);
        var executionService = new BackupExecutionService(fileSystem, options);
        var logger = NullLogger<BackupOrchestrator>.Instance;

        return new BackupOrchestrator(
            snapshotService, planService, overwriteDetection,
            executionService, fileSystem, logger);
    }

    private void CreateSourceFile(string relativePath, string content)
    {
        string full = Path.Combine(_sourceDir, relativePath);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);
    }

    private void CreateDestFile(string relativePath, string content)
    {
        string full = Path.Combine(_destDir, relativePath);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(full, content);
    }

    [Test]
    public async Task SyncMode_NewDestination_CopiesAllFilesAndDirectories()
    {
        CreateSourceFile("file1.txt", "hello");
        CreateSourceFile("sub/file2.txt", "world");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(2));
            Assert.That(result.DirectoriesCreated, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(_destDir, "file1.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(_destDir, "sub", "file2.txt")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(_destDir, "file1.txt")), Is.EqualTo("hello"));
            Assert.That(File.ReadAllText(Path.Combine(_destDir, "sub", "file2.txt")), Is.EqualTo("world"));
        });
    }

    [Test]
    public async Task SyncMode_DeletesExtraFilesAndDirectories()
    {
        CreateSourceFile("keep.txt", "keep");

        Directory.CreateDirectory(_destDir);
        CreateDestFile("keep.txt", "keep");
        CreateDestFile("extra.txt", "delete me");
        CreateDestFile("extradir/nested.txt", "delete me too");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesDeleted, Is.GreaterThanOrEqualTo(1));
            Assert.That(File.Exists(Path.Combine(_destDir, "keep.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(_destDir, "extra.txt")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_destDir, "extradir")), Is.False);
        });
    }

    [Test]
    public async Task SyncMode_OverwritesChangedFile()
    {
        CreateSourceFile("data.txt", "updated content");

        Directory.CreateDirectory(_destDir);
        CreateDestFile("data.txt", "old content");

        // Ensure different timestamps
        File.SetLastWriteTimeUtc(
            Path.Combine(_sourceDir, "data.txt"),
            DateTime.UtcNow);
        File.SetLastWriteTimeUtc(
            Path.Combine(_destDir, "data.txt"),
            DateTime.UtcNow.AddDays(-1));

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesOverwritten, Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(_destDir, "data.txt")),
                Is.EqualTo("updated content"));
        });
    }

    [Test]
    public async Task SyncMode_IdenticalFiles_NotOverwritten()
    {
        CreateSourceFile("same.txt", "identical");

        Directory.CreateDirectory(_destDir);
        CreateDestFile("same.txt", "identical");

        // Set identical timestamps and sizes
        DateTime now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(_sourceDir, "same.txt"), now);
        File.SetLastWriteTimeUtc(Path.Combine(_destDir, "same.txt"), now);

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.That(result.FilesOverwritten, Is.EqualTo(0));
    }

    [Test]
    public async Task AddMode_CopiesNewFilesOnly_DoesNotDeleteExistingDestFiles()
    {
        CreateSourceFile("new.txt", "new file");
        CreateSourceFile("shared.txt", "source version");

        Directory.CreateDirectory(_destDir);
        CreateDestFile("shared.txt", "dest version");
        CreateDestFile("destonly.txt", "should remain");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Add);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(1));
            Assert.That(result.FilesDeleted, Is.EqualTo(0));
            Assert.That(result.DirectoriesDeleted, Is.EqualTo(0));
            Assert.That(File.Exists(Path.Combine(_destDir, "new.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(_destDir, "destonly.txt")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(_destDir, "shared.txt")),
                Is.EqualTo("dest version"));
        });
    }

    [Test]
    public async Task AddMode_NewDestination_CopiesAllFiles()
    {
        CreateSourceFile("a.txt", "aaa");
        CreateSourceFile("dir/b.txt", "bbb");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Add);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(2));
            Assert.That(result.DirectoriesCreated, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(_destDir, "a.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(_destDir, "dir", "b.txt")), Is.True);
        });
    }

    [Test]
    public async Task RemoveMode_DeletesExtraFiles_DoesNotCopy()
    {
        CreateSourceFile("keep.txt", "keep me");

        Directory.CreateDirectory(_destDir);
        CreateDestFile("keep.txt", "dest keep");
        CreateDestFile("remove.txt", "remove me");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Remove);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(0));
            Assert.That(result.FilesDeleted, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(_destDir, "keep.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(_destDir, "remove.txt")), Is.False);
        });
    }

    [Test]
    public async Task RemoveMode_DeletesExtraDirectories()
    {
        CreateSourceFile("root.txt", "root");

        Directory.CreateDirectory(_destDir);
        CreateDestFile("root.txt", "root");
        CreateDestFile("extradir/nested.txt", "nested");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Remove);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.DirectoriesDeleted, Is.GreaterThanOrEqualTo(1));
            Assert.That(Directory.Exists(Path.Combine(_destDir, "extradir")), Is.False);
        });
    }

    [Test]
    public async Task RemoveMode_DestinationDoesNotExist_ReturnsZeroCounts()
    {
        CreateSourceFile("file.txt", "data");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Remove);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(0));
            Assert.That(result.FilesDeleted, Is.EqualTo(0));
            Assert.That(result.DirectoriesDeleted, Is.EqualTo(0));
        });
    }

    [Test]
    public void SourceDoesNotExist_ThrowsDirectoryNotFoundException()
    {
        string nonExistent = Path.Combine(_testRoot, "nonexistent");
        Directory.CreateDirectory(_destDir);

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(nonExistent, _destDir, BackupMode.Sync);

        Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => orchestrator.ExecuteAsync(request, null, CancellationToken.None));
    }

    [Test]
    public void OverlappingPaths_ThrowsInvalidOperationException()
    {
        string nested = Path.Combine(_sourceDir, "nested");
        Directory.CreateDirectory(nested);

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, nested, BackupMode.Sync);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => orchestrator.ExecuteAsync(request, null, CancellationToken.None));
    }

    [Test]
    public void Cancellation_ThrowsOperationCanceledException()
    {
        // Create enough files to trigger cancellation check
        for (int i = 0; i < 10; i++)
            CreateSourceFile($"file{i}.txt", $"content{i}");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        // TaskCanceledException derives from OperationCanceledException
        Assert.That(
            async () => await orchestrator.ExecuteAsync(request, null, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task SyncMode_CopiesFileContentCorrectly()
    {
        CreateSourceFile("timed.txt", "timed content");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(1));
            Assert.That(File.ReadAllText(Path.Combine(_destDir, "timed.txt")),
                Is.EqualTo("timed content"));
        });
    }

    [Test]
    public async Task SyncMode_DeepNestedStructure_CopiesCorrectly()
    {
        CreateSourceFile("a/b/c/d/deep.txt", "deep content");
        CreateSourceFile("a/b/sibling.txt", "sibling");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(_destDir, "a", "b", "c", "d", "deep.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(_destDir, "a", "b", "sibling.txt")), Is.True);
        });
    }

    [Test]
    public async Task SyncMode_EmptySource_CleansDestination()
    {
        // Source is empty (just the directory)
        Directory.CreateDirectory(_destDir);
        CreateDestFile("old.txt", "old");
        CreateDestFile("olddir/nested.txt", "nested");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(request, null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesDeleted, Is.GreaterThanOrEqualTo(1));
            Assert.That(File.Exists(Path.Combine(_destDir, "old.txt")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_destDir, "olddir")), Is.False);
        });
    }

    [Test]
    public async Task ProgressReporting_ReceivesProgressUpdates()
    {
        CreateSourceFile("file1.txt", "content1");
        CreateSourceFile("file2.txt", "content2");

        var reported = new List<BackupProgress>();
        var progress = new Progress<BackupProgress>(p => reported.Add(p));

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(_sourceDir, _destDir, BackupMode.Sync);

        await orchestrator.ExecuteAsync(request, progress, CancellationToken.None);

        // Allow async progress callbacks to be delivered
        await Task.Delay(200);

        Assert.That(reported, Is.Not.Empty);
    }
}

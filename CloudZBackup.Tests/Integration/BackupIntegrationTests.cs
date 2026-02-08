namespace CloudZBackup.Tests.Integration;

using CloudZBackup.Application.Orchestrators;
using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// End-to-end integration tests that exercise the full backup pipeline
/// using real file system operations on temporary directories.
/// </summary>
[TestFixture]
public sealed class BackupIntegrationTests
{
    private string destDir = null!;
    private string sourceDir = null!;
    private string testRoot = null!;

    /// <summary>
    /// Verifies that add mode copies only new files to the destination and does not
    /// delete or overwrite existing destination files.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddModeCopiesNewFilesOnlyDoesNotDeleteExistingDestFiles()
    {
        await this.CreateSourceFile("new.txt", "new file");
        await this.CreateSourceFile("shared.txt", "source version");

        Directory.CreateDirectory(this.destDir);
        await this.CreateDestFile("shared.txt", "dest version");
        await this.CreateDestFile("destonly.txt", "should remain");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Add);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(1));
            Assert.That(result.FilesDeleted, Is.EqualTo(0));
            Assert.That(result.DirectoriesDeleted, Is.EqualTo(0));
            Assert.That(File.Exists(Path.Combine(this.destDir, "new.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(this.destDir, "destonly.txt")), Is.True);
            Assert.That(
                File.ReadAllText(Path.Combine(this.destDir, "shared.txt")),
                Is.EqualTo("dest version"));
        });
    }

    /// <summary>
    /// Verifies that add mode copies all files when the destination does not yet exist.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddModeNewDestinationCopiesAllFiles()
    {
        await this.CreateSourceFile("a.txt", "aaa");
        await this.CreateSourceFile("dir/b.txt", "bbb");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Add);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(2));
            Assert.That(result.DirectoriesCreated, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(this.destDir, "a.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(this.destDir, "dir", "b.txt")), Is.True);
        });
    }

    /// <summary>
    /// Verifies that a pre-cancelled token causes an <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task CancellationThrowsOperationCanceledException()
    {
        // Create enough files to trigger cancellation check
        for (int i = 0; i < 10; i++)
        {
            await this.CreateSourceFile($"file{i}.txt", $"content{i}");
        }

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        // TaskCanceledException derives from OperationCanceledException
        await Assert.ThatAsync(
            async () => await orchestrator.ExecuteAsync(request, null, cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    /// <summary>
    /// Verifies that overlapping source and destination paths throw
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    [Test]
    public void OverlappingPathsThrowsInvalidOperationException()
    {
        string nested = Path.Combine(this.sourceDir, "nested");
        Directory.CreateDirectory(nested);

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, nested, BackupMode.Sync);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync(request, null, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that the orchestrator reports progress updates during a backup operation.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ProgressReportingReceivesProgressUpdates()
    {
        await this.CreateSourceFile("file1.txt", "content1");
        await this.CreateSourceFile("file2.txt", "content2");

        var reported = new List<BackupProgress>();
        var progress = new Progress<BackupProgress>(p => reported.Add(p));

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        await orchestrator.ExecuteAsync(request, progress, CancellationToken.None);

        // Allow async progress callbacks to be delivered
        await Task.Delay(200);

        Assert.That(reported, Is.Not.Empty);
    }

    /// <summary>
    /// Verifies that remove mode deletes directories in the destination that do not exist
    /// in the source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RemoveModeDeletesExtraDirectories()
    {
        await this.CreateSourceFile("root.txt", "root");

        Directory.CreateDirectory(this.destDir);
        await this.CreateDestFile("root.txt", "root");
        await this.CreateDestFile("extradir/nested.txt", "nested");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Remove);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.DirectoriesDeleted, Is.GreaterThanOrEqualTo(1));
            Assert.That(Directory.Exists(Path.Combine(this.destDir, "extradir")), Is.False);
        });
    }

    /// <summary>
    /// Verifies that remove mode deletes extra files from the destination without
    /// copying new files.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RemoveModeDeletesExtraFilesDoesNotCopy()
    {
        await this.CreateSourceFile("keep.txt", "keep me");

        Directory.CreateDirectory(this.destDir);
        await this.CreateDestFile("keep.txt", "dest keep");
        await this.CreateDestFile("remove.txt", "remove me");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Remove);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(0));
            Assert.That(result.FilesDeleted, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(this.destDir, "keep.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(this.destDir, "remove.txt")), Is.False);
        });
    }

    /// <summary>
    /// Verifies that remove mode returns zero counts when the destination does not exist.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task RemoveModeDestinationDoesNotExistReturnsZeroCounts()
    {
        await this.CreateSourceFile("file.txt", "data");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Remove);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(0));
            Assert.That(result.FilesDeleted, Is.EqualTo(0));
            Assert.That(result.DirectoriesDeleted, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// Creates unique temporary source and destination directories before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.testRoot = Path.Combine(
            Path.GetTempPath(),
            "CloudZBackupTests",
            Guid.NewGuid().ToString("N"));
        this.sourceDir = Path.Combine(this.testRoot, "source");
        this.destDir = Path.Combine(this.testRoot, "dest");
        Directory.CreateDirectory(this.sourceDir);
    }

    /// <summary>
    /// Verifies that a non-existent source directory throws <see cref="DirectoryNotFoundException"/>.
    /// </summary>
    [Test]
    public void SourceDoesNotExistThrowsDirectoryNotFoundException()
    {
        string nonExistent = Path.Combine(this.testRoot, "nonexistent");
        Directory.CreateDirectory(this.destDir);

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(nonExistent, this.destDir, BackupMode.Sync);

        Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            orchestrator.ExecuteAsync(request, null, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that sync mode copies file content correctly to the destination.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SyncModeCopiesFileContentCorrectly()
    {
        await this.CreateSourceFile("timed.txt", "timed content");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(1));
            Assert.That(
                File.ReadAllText(Path.Combine(this.destDir, "timed.txt")),
                Is.EqualTo("timed content"));
        });
    }

    /// <summary>
    /// Verifies that sync mode correctly copies a deeply nested directory structure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SyncModeDeepNestedStructureCopiesCorrectly()
    {
        await this.CreateSourceFile("a/b/c/d/deep.txt", "deep content");
        await this.CreateSourceFile("a/b/sibling.txt", "sibling");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(2));
            Assert.That(
                File.Exists(Path.Combine(this.destDir, "a", "b", "c", "d", "deep.txt")),
                Is.True);
            Assert.That(File.Exists(Path.Combine(this.destDir, "a", "b", "sibling.txt")), Is.True);
        });
    }

    /// <summary>
    /// Verifies that sync mode deletes extra files and directories that are not present
    /// in the source.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SyncModeDeletesExtraFilesAndDirectories()
    {
        await this.CreateSourceFile("keep.txt", "keep");

        Directory.CreateDirectory(this.destDir);
        await this.CreateDestFile("keep.txt", "keep");
        await this.CreateDestFile("extra.txt", "delete me");
        await this.CreateDestFile("extradir/nested.txt", "delete me too");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesDeleted, Is.GreaterThanOrEqualTo(1));
            Assert.That(File.Exists(Path.Combine(this.destDir, "keep.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(this.destDir, "extra.txt")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(this.destDir, "extradir")), Is.False);
        });
    }

    /// <summary>
    /// Verifies that sync mode with an empty source cleans all files and directories
    /// from the destination.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SyncModeEmptySourceCleansDestination()
    {
        // Source is empty (just the directory)
        Directory.CreateDirectory(this.destDir);
        await this.CreateDestFile("old.txt", "old");
        await this.CreateDestFile("olddir/nested.txt", "nested");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesDeleted, Is.GreaterThanOrEqualTo(1));
            Assert.That(File.Exists(Path.Combine(this.destDir, "old.txt")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(this.destDir, "olddir")), Is.False);
        });
    }

    /// <summary>
    /// Verifies that sync mode does not overwrite files when source and destination
    /// have identical content and timestamps.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SyncModeIdenticalFilesNotOverwritten()
    {
        await this.CreateSourceFile("same.txt", "identical");

        Directory.CreateDirectory(this.destDir);
        await this.CreateDestFile("same.txt", "identical");

        // Set identical timestamps and sizes
        DateTime now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(this.sourceDir, "same.txt"), now);
        File.SetLastWriteTimeUtc(Path.Combine(this.destDir, "same.txt"), now);

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.That(result.FilesOverwritten, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that sync mode copies all files and creates all directories when the
    /// destination is new.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SyncModeNewDestinationCopiesAllFilesAndDirectories()
    {
        await this.CreateSourceFile("file1.txt", "hello");
        await this.CreateSourceFile("sub/file2.txt", "world");

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesCopied, Is.EqualTo(2));
            Assert.That(result.DirectoriesCreated, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(this.destDir, "file1.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(this.destDir, "sub", "file2.txt")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(this.destDir, "file1.txt")), Is.EqualTo("hello"));
            Assert.That(
                File.ReadAllText(Path.Combine(this.destDir, "sub", "file2.txt")),
                Is.EqualTo("world"));
        });
    }

    /// <summary>
    /// Verifies that sync mode overwrites a file in the destination when the source
    /// contains updated content with a different timestamp.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task SyncModeOverwritesChangedFile()
    {
        await this.CreateSourceFile("data.txt", "updated content");

        Directory.CreateDirectory(this.destDir);
        await this.CreateDestFile("data.txt", "old content");

        // Ensure different timestamps
        File.SetLastWriteTimeUtc(Path.Combine(this.sourceDir, "data.txt"), DateTime.UtcNow);
        File.SetLastWriteTimeUtc(Path.Combine(this.destDir, "data.txt"), DateTime.UtcNow.AddDays(-1));

        var orchestrator = CreateOrchestrator();
        var request = new BackupRequest(this.sourceDir, this.destDir, BackupMode.Sync);

        BackupResult result = await orchestrator.ExecuteAsync(
            request,
            null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesOverwritten, Is.EqualTo(1));
            Assert.That(
                File.ReadAllText(Path.Combine(this.destDir, "data.txt")),
                Is.EqualTo("updated content"));
        });
    }

    /// <summary>
    /// Cleans up the temporary test directories after each test.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this.testRoot))
        {
            Directory.Delete(this.testRoot, recursive: true);
        }
    }

    private static BackupOrchestrator CreateOrchestrator()
    {
        IFileSystemService fileSystem = new FileSystemService();
        IHashingService hashing = new HashingService();
        var options = Options.Create(
            new BackupOptions { MaxHashConcurrency = 1, MaxFileIoConcurrency = 1 });

        var snapshotService = new SnapshotService(fileSystem);
        var planService = new PlanService();
        var overwriteDetection = new OverwriteDetectionService(hashing, options, fileSystem);
        var executionService = new BackupExecutionService(fileSystem, options);
        var logger = NullLogger<BackupOrchestrator>.Instance;

        return new BackupOrchestrator(
            snapshotService,
            planService,
            overwriteDetection,
            executionService,
            fileSystem,
            logger);
    }

    private async Task CreateDestFile(string relativePath, string content)
    {
        string full = Path.Combine(this.destDir, relativePath);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(full, content);
    }

    private async Task CreateSourceFile(string relativePath, string content)
    {
        string full = Path.Combine(this.sourceDir, relativePath);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(full, content);
    }
}

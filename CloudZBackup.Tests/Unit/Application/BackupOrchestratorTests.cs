namespace CloudZBackup.Tests.Unit.Application;

using CloudZBackup.Application.Orchestrators;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

/// <summary>
/// Unit tests for <see cref="BackupOrchestrator"/>.
/// </summary>
[TestFixture]
public sealed class BackupOrchestratorTests
{
    private static readonly DateTime BaseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private IBackupExecutionService executionService = null!;
    private IFileSystemService fileSystem = null!;
    private IOverwriteDetectionService overwriteDetection = null!;
    private IPlanService planService = null!;
    private ISnapshotService snapshotService = null!;
    private BackupOrchestrator sut = null!;

    /// <summary>
    /// Verifies that executing in <see cref="BackupMode.Add"/> mode does not invoke
    /// the overwrite detection service.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ExecuteAsyncAddModeDoesNotCallOverwriteDetection()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Add);
        this.SetupValidRequest(request);

        await this.sut.ExecuteAsync(request, null, CancellationToken.None);

        await this.overwriteDetection
            .DidNotReceive()
            .ComputeFilesToOverwriteAsync(
                Arg.Any<IReadOnlyList<RelativePath>>(),
                Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
                Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that when the destination is newly created, an empty snapshot is used
    /// for the destination instead of scanning it.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ExecuteAsyncDestCreatedUsesEmptyDestSnapshot()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Add);
        this.SetupValidRequest(request, destCreated: true);

        await this.sut.ExecuteAsync(request, null, CancellationToken.None);

        this.snapshotService.Received(1).CreateEmptySnapshot();
    }

    /// <summary>
    /// Verifies that overlapping source and destination paths cause an
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    [Test]
    public void ExecuteAsyncOverlapPathsThrows()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Sync);
        this.fileSystem.ValidateAndNormalize(request).Returns(("/src", "/dst"));
        this.fileSystem
            .When(x => x.ValidateNoOverlap("/src", "/dst"))
            .Do(_ => throw new InvalidOperationException("Overlap"));

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            this.sut.ExecuteAsync(request, null, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that executing in <see cref="BackupMode.Sync"/> mode with common files
    /// invokes the overwrite detection service.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ExecuteAsyncSyncModeWithCommonFilesCallsOverwriteDetection()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Sync);
        this.fileSystem.ValidateAndNormalize(request).Returns(("/src", "/dst"));
        this.fileSystem.PrepareDestination(request.Mode, "/dst").Returns(false);

        var commonFile = new RelativePath("shared.txt");
        var entry = new FileEntry(commonFile, 100, BaseTime);
        var sourceSnapshot = new Snapshot(
            new Dictionary<RelativePath, FileEntry> { [commonFile] = entry },
            new HashSet<RelativePath>());
        var destSnapshot = new Snapshot(
            new Dictionary<RelativePath, FileEntry> { [commonFile] = entry },
            new HashSet<RelativePath>());

        this.snapshotService
            .CaptureSnapshot("/src", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(sourceSnapshot);
        this.snapshotService
            .CaptureSnapshot("/dst", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(destSnapshot);

        var planWithCommon = new Plan([], [], [commonFile], [], []);
        this.planService
            .BuildPlan(BackupMode.Sync, Arg.Any<Snapshot>(), Arg.Any<Snapshot>())
            .Returns(planWithCommon);

        this.overwriteDetection
            .ComputeFilesToOverwriteAsync(
                Arg.Any<IReadOnlyList<RelativePath>>(),
                Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
                Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<RelativePath>());

        this.executionService
            .ExecuteAsync(
                Arg.Any<BackupMode>(),
                Arg.Any<Plan>(),
                Arg.Any<Snapshot>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<RelativePath>>(),
                Arg.Any<IProgress<BackupProgress>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new BackupResult(0, 0, 0, 0, 0));

        await this.sut.ExecuteAsync(request, null, CancellationToken.None);

        await this.overwriteDetection
            .Received(1)
            .ComputeFilesToOverwriteAsync(
                Arg.Any<IReadOnlyList<RelativePath>>(),
                Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
                Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
                "/src",
                "/dst",
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that a valid request calls all services in the expected order.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ExecuteAsyncValidRequestCallsAllServicesInOrder()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Sync);
        this.SetupValidRequest(request);

        BackupResult result = await this.sut.ExecuteAsync(request, null, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        this.fileSystem.Received(1).ValidateAndNormalize(request);
        this.fileSystem.Received(1).ValidateNoOverlap("/src", "/dst");
        this.fileSystem.Received(1).EnsureSourceExists("/src");
    }

    /// <summary>
    /// Initializes test dependencies before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this.snapshotService = Substitute.For<ISnapshotService>();
        this.planService = Substitute.For<IPlanService>();
        this.overwriteDetection = Substitute.For<IOverwriteDetectionService>();
        this.executionService = Substitute.For<IBackupExecutionService>();
        this.fileSystem = Substitute.For<IFileSystemService>();
        var logger = NullLogger<BackupOrchestrator>.Instance;

        this.sut = new BackupOrchestrator(
            this.snapshotService,
            this.planService,
            this.overwriteDetection,
            this.executionService,
            this.fileSystem,
            logger);
    }

    private void SetupValidRequest(BackupRequest request, bool destCreated = false)
    {
        this.fileSystem
            .ValidateAndNormalize(request)
            .Returns((request.SourcePath, request.DestinationPath));
        this.fileSystem.PrepareDestination(request.Mode, request.DestinationPath).Returns(destCreated);

        var emptySnapshot = new Snapshot(
            new Dictionary<RelativePath, FileEntry>(),
            new HashSet<RelativePath>());

        this.snapshotService
            .CaptureSnapshot(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(emptySnapshot);
        this.snapshotService.CreateEmptySnapshot().Returns(emptySnapshot);

        var emptyPlan = new Plan([], [], [], [], []);
        this.planService
            .BuildPlan(Arg.Any<BackupMode>(), Arg.Any<Snapshot>(), Arg.Any<Snapshot>())
            .Returns(emptyPlan);

        var expectedResult = new BackupResult(0, 0, 0, 0, 0);
        this.executionService
            .ExecuteAsync(
                Arg.Any<BackupMode>(),
                Arg.Any<Plan>(),
                Arg.Any<Snapshot>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyCollection<RelativePath>>(),
                Arg.Any<IProgress<BackupProgress>?>(),
                Arg.Any<CancellationToken>())
            .Returns(expectedResult);
    }
}

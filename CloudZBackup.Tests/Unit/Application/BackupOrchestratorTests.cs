using CloudZBackup.Application.Orchestrators;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CloudZBackup.Tests.Unit.Application;

[TestFixture]
public sealed class BackupOrchestratorTests
{
    private ISnapshotService _snapshotService = null!;
    private IPlanService _planService = null!;
    private IOverwriteDetectionService _overwriteDetection = null!;
    private IBackupExecutionService _executionService = null!;
    private IFileSystemService _fileSystem = null!;
    private BackupOrchestrator _sut = null!;

    private static readonly DateTime BaseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _snapshotService = Substitute.For<ISnapshotService>();
        _planService = Substitute.For<IPlanService>();
        _overwriteDetection = Substitute.For<IOverwriteDetectionService>();
        _executionService = Substitute.For<IBackupExecutionService>();
        _fileSystem = Substitute.For<IFileSystemService>();
        var logger = NullLogger<BackupOrchestrator>.Instance;

        _sut = new BackupOrchestrator(
            _snapshotService, _planService, _overwriteDetection,
            _executionService, _fileSystem, logger);
    }

    private void SetupValidRequest(BackupRequest request, bool destCreated = false)
    {
        _fileSystem.ValidateAndNormalize(request)
            .Returns((request.SourcePath, request.DestinationPath));
        _fileSystem.PrepareDestination(request.Mode, request.DestinationPath)
            .Returns(destCreated);

        var emptySnapshot = new Snapshot(
            new Dictionary<RelativePath, FileEntry>(),
            new HashSet<RelativePath>());

        _snapshotService.CaptureSnapshot(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(emptySnapshot);
        _snapshotService.CreateEmptySnapshot().Returns(emptySnapshot);

        var emptyPlan = new Plan([], [], [], [], []);
        _planService.BuildPlan(Arg.Any<BackupMode>(), Arg.Any<Snapshot>(), Arg.Any<Snapshot>())
            .Returns(emptyPlan);

        var expectedResult = new BackupResult(0, 0, 0, 0, 0);
        _executionService.ExecuteAsync(
            Arg.Any<BackupMode>(), Arg.Any<Plan>(), Arg.Any<Snapshot>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<RelativePath>>(),
            Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);
    }

    [Test]
    public async Task ExecuteAsync_ValidRequest_CallsAllServicesInOrder()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Sync);
        SetupValidRequest(request);

        BackupResult result = await _sut.ExecuteAsync(request, null, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        _fileSystem.Received(1).ValidateAndNormalize(request);
        _fileSystem.Received(1).ValidateNoOverlap("/src", "/dst");
        _fileSystem.Received(1).EnsureSourceExists("/src");
    }

    [Test]
    public async Task ExecuteAsync_DestCreated_UsesEmptyDestSnapshot()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Add);
        SetupValidRequest(request, destCreated: true);

        await _sut.ExecuteAsync(request, null, CancellationToken.None);

        _snapshotService.Received(1).CreateEmptySnapshot();
    }

    [Test]
    public async Task ExecuteAsync_SyncModeWithCommonFiles_CallsOverwriteDetection()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Sync);
        _fileSystem.ValidateAndNormalize(request).Returns(("/src", "/dst"));
        _fileSystem.PrepareDestination(request.Mode, "/dst").Returns(false);

        var commonFile = new RelativePath("shared.txt");
        var entry = new FileEntry(commonFile, 100, BaseTime);
        var sourceSnapshot = new Snapshot(
            new Dictionary<RelativePath, FileEntry> { [commonFile] = entry },
            new HashSet<RelativePath>());
        var destSnapshot = new Snapshot(
            new Dictionary<RelativePath, FileEntry> { [commonFile] = entry },
            new HashSet<RelativePath>());

        _snapshotService.CaptureSnapshot("/src", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(sourceSnapshot);
        _snapshotService.CaptureSnapshot("/dst", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(destSnapshot);

        var planWithCommon = new Plan([], [], [commonFile], [], []);
        _planService.BuildPlan(BackupMode.Sync, Arg.Any<Snapshot>(), Arg.Any<Snapshot>())
            .Returns(planWithCommon);

        _overwriteDetection.ComputeFilesToOverwriteAsync(
            Arg.Any<IReadOnlyList<RelativePath>>(),
            Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
            Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<RelativePath>());

        _executionService.ExecuteAsync(
            Arg.Any<BackupMode>(), Arg.Any<Plan>(), Arg.Any<Snapshot>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<RelativePath>>(),
            Arg.Any<IProgress<BackupProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(new BackupResult(0, 0, 0, 0, 0));

        await _sut.ExecuteAsync(request, null, CancellationToken.None);

        await _overwriteDetection.Received(1).ComputeFilesToOverwriteAsync(
            Arg.Any<IReadOnlyList<RelativePath>>(),
            Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
            Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
            "/src", "/dst", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_AddMode_DoesNotCallOverwriteDetection()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Add);
        SetupValidRequest(request);

        await _sut.ExecuteAsync(request, null, CancellationToken.None);

        await _overwriteDetection.DidNotReceive().ComputeFilesToOverwriteAsync(
            Arg.Any<IReadOnlyList<RelativePath>>(),
            Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
            Arg.Any<IReadOnlyDictionary<RelativePath, FileEntry>>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void ExecuteAsync_OverlapPaths_Throws()
    {
        var request = new BackupRequest("/src", "/dst", BackupMode.Sync);
        _fileSystem.ValidateAndNormalize(request).Returns(("/src", "/dst"));
        _fileSystem.When(x => x.ValidateNoOverlap("/src", "/dst"))
            .Do(_ => throw new InvalidOperationException("Overlap"));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ExecuteAsync(request, null, CancellationToken.None));
    }
}

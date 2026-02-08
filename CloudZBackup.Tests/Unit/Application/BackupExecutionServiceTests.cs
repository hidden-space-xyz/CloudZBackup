using CloudZBackup.Application.Services;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CloudZBackup.Tests.Unit.Application;

[TestFixture]
public sealed class BackupExecutionServiceTests
{
    private IFileSystemService _fileSystem = null!;
    private BackupExecutionService _sut = null!;

    private static readonly DateTime BaseTime = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _fileSystem = Substitute.For<IFileSystemService>();
        var options = Options.Create(new BackupOptions { MaxFileIoConcurrency = 1 });
        _sut = new BackupExecutionService(_fileSystem, options);

        _fileSystem.Combine(Arg.Any<string>(), Arg.Any<RelativePath>())
            .Returns(ci => $"{ci.ArgAt<string>(0)}/{ci.ArgAt<RelativePath>(1).Value}");
    }

    private static Plan CreateEmptyPlan() =>
        new([], [], [], [], []);

    private static Snapshot CreateSnapshot(params string[] filePaths)
    {
        var files = new Dictionary<RelativePath, FileEntry>();
        foreach (string f in filePaths)
        {
            var rp = new RelativePath(f);
            files[rp] = new FileEntry(rp, 100, BaseTime);
        }
        return new Snapshot(files, new HashSet<RelativePath>());
    }

    [Test]
    public async Task Execute_AddMode_CreatesDirectoriesAndCopiesFiles()
    {
        var dirToCreate = new RelativePath("newdir");
        var fileToCopy = new RelativePath("newfile.txt");
        var plan = new Plan([dirToCreate], [fileToCopy], [], [], []);
        Snapshot source = CreateSnapshot("newfile.txt");

        BackupResult result = await _sut.ExecuteAsync(
            BackupMode.Add, plan, source, "/src", "/dst",
            [], null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.DirectoriesCreated, Is.EqualTo(1));
            Assert.That(result.FilesCopied, Is.EqualTo(1));
            Assert.That(result.FilesOverwritten, Is.EqualTo(0));
            Assert.That(result.FilesDeleted, Is.EqualTo(0));
            Assert.That(result.DirectoriesDeleted, Is.EqualTo(0));
        });

        _fileSystem.Received(1).CreateDirectory("/dst/newdir");
        await _fileSystem.Received(1).CopyFileAsync(
            "/src/newfile.txt", "/dst/newfile.txt",
            false, BaseTime, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Execute_SyncMode_PerformsAllOperations()
    {
        var dirToCreate = new RelativePath("newdir");
        var missingFile = new RelativePath("new.txt");
        var commonFile = new RelativePath("common.txt");
        var extraFile = new RelativePath("extra.txt");
        var extraDir = new RelativePath("olddir");
        var fileToOverwrite = new RelativePath("common.txt");

        var plan = new Plan([dirToCreate], [missingFile], [commonFile], [extraFile], [extraDir]);
        Snapshot source = CreateSnapshot("new.txt", "common.txt");

        BackupResult result = await _sut.ExecuteAsync(
            BackupMode.Sync, plan, source, "/src", "/dst",
            [fileToOverwrite], null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.DirectoriesCreated, Is.EqualTo(1));
            Assert.That(result.FilesCopied, Is.EqualTo(1));
            Assert.That(result.FilesOverwritten, Is.EqualTo(1));
            Assert.That(result.FilesDeleted, Is.EqualTo(1));
            Assert.That(result.DirectoriesDeleted, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Execute_RemoveMode_DeletesExtraFilesAndDirectories()
    {
        var extraFile = new RelativePath("old.txt");
        var extraDir = new RelativePath("olddir");
        var plan = new Plan([], [], [], [extraFile], [extraDir]);
        Snapshot source = CreateSnapshot();

        BackupResult result = await _sut.ExecuteAsync(
            BackupMode.Remove, plan, source, "/src", "/dst",
            [], null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.DirectoriesCreated, Is.EqualTo(0));
            Assert.That(result.FilesCopied, Is.EqualTo(0));
            Assert.That(result.FilesDeleted, Is.EqualTo(1));
            Assert.That(result.DirectoriesDeleted, Is.EqualTo(1));
        });

        _fileSystem.Received(1).DeleteFileIfExists("/dst/old.txt");
        _fileSystem.Received(1).DeleteDirectoryIfExists("/dst/olddir", recursive: true);
    }

    [Test]
    public async Task Execute_EmptyPlan_ReturnsZeroCounts()
    {
        BackupResult result = await _sut.ExecuteAsync(
            BackupMode.Sync, CreateEmptyPlan(), CreateSnapshot(),
            "/src", "/dst", [], null, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.DirectoriesCreated, Is.EqualTo(0));
            Assert.That(result.FilesCopied, Is.EqualTo(0));
            Assert.That(result.FilesOverwritten, Is.EqualTo(0));
            Assert.That(result.FilesDeleted, Is.EqualTo(0));
            Assert.That(result.DirectoriesDeleted, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Execute_ReportsProgress()
    {
        var missingFile = new RelativePath("file.txt");
        var plan = new Plan([], [missingFile], [], [], []);
        Snapshot source = CreateSnapshot("file.txt");

        var reported = new List<BackupProgress>();
        var progress = new Progress<BackupProgress>(p => reported.Add(p));

        await _sut.ExecuteAsync(
            BackupMode.Add, plan, source, "/src", "/dst",
            [], progress, CancellationToken.None);

        // Allow progress callbacks to be delivered (they may be posted asynchronously)
        await Task.Delay(100);

        Assert.That(reported, Is.Not.Empty);
    }
}

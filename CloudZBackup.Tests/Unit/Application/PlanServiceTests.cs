using CloudZBackup.Application.Services;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Comparers;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Tests.Unit.Application;

[TestFixture]
public sealed class PlanServiceTests
{
    private readonly PlanService _sut = new();
    private readonly IEqualityComparer<RelativePath> _comparer =
        new RelativePathComparer(OperatingSystem.IsWindows());

    private Snapshot CreateSnapshot(
        IEnumerable<string>? filePaths = null,
        IEnumerable<string>? dirPaths = null)
    {
        var files = new Dictionary<RelativePath, FileEntry>(_comparer);
        var dirs = new HashSet<RelativePath>(_comparer);

        foreach (string f in filePaths ?? [])
        {
            var rp = new RelativePath(f);
            files[rp] = new FileEntry(rp, 100, DateTime.UtcNow);
        }

        foreach (string d in dirPaths ?? [])
            dirs.Add(new RelativePath(d));

        return new Snapshot(files, dirs);
    }

    [Test]
    public void BuildPlan_SyncMode_IdentifiesMissingCommonAndExtraFiles()
    {
        Snapshot source = CreateSnapshot(
            filePaths: ["shared.txt", "new.txt"],
            dirPaths: ["dirA"]
        );
        Snapshot dest = CreateSnapshot(
            filePaths: ["shared.txt", "old.txt"],
            dirPaths: ["dirB"]
        );

        Plan plan = _sut.BuildPlan(BackupMode.Sync, source, dest);

        Assert.Multiple(() =>
        {
            Assert.That(plan.MissingFiles.Select(f => f.Value), Does.Contain("new.txt"));
            Assert.That(plan.CommonFiles.Select(f => f.Value), Does.Contain("shared.txt"));
            Assert.That(plan.ExtraFiles.Select(f => f.Value), Does.Contain("old.txt"));
            Assert.That(plan.DirectoriesToCreate.Select(d => d.Value), Does.Contain("dirA"));
        });
    }

    [Test]
    public void BuildPlan_AddMode_OnlyIdentifiesMissingFilesAndDirs_NoExtras()
    {
        Snapshot source = CreateSnapshot(
            filePaths: ["shared.txt", "new.txt"],
            dirPaths: ["dirA"]
        );
        Snapshot dest = CreateSnapshot(
            filePaths: ["shared.txt", "old.txt"],
            dirPaths: ["dirB"]
        );

        Plan plan = _sut.BuildPlan(BackupMode.Add, source, dest);

        Assert.Multiple(() =>
        {
            Assert.That(plan.MissingFiles.Select(f => f.Value), Does.Contain("new.txt"));
            Assert.That(plan.CommonFiles, Is.Empty);
            Assert.That(plan.ExtraFiles, Is.Empty);
            Assert.That(plan.TopLevelExtraDirectories, Is.Empty);
            Assert.That(plan.DirectoriesToCreate.Select(d => d.Value), Does.Contain("dirA"));
        });
    }

    [Test]
    public void BuildPlan_RemoveMode_OnlyIdentifiesExtraFilesAndDirs()
    {
        Snapshot source = CreateSnapshot(
            filePaths: ["shared.txt"],
            dirPaths: ["dirA"]
        );
        Snapshot dest = CreateSnapshot(
            filePaths: ["shared.txt", "extra.txt"],
            dirPaths: ["dirA", "extraDir"]
        );

        Plan plan = _sut.BuildPlan(BackupMode.Remove, source, dest);

        Assert.Multiple(() =>
        {
            Assert.That(plan.MissingFiles, Is.Empty);
            Assert.That(plan.CommonFiles, Is.Empty);
            Assert.That(plan.DirectoriesToCreate, Is.Empty);
            Assert.That(plan.ExtraFiles.Select(f => f.Value), Does.Contain("extra.txt"));
            Assert.That(plan.TopLevelExtraDirectories.Select(d => d.Value), Does.Contain("extraDir"));
        });
    }

    [Test]
    public void BuildPlan_EmptySnapshots_ReturnsEmptyPlan()
    {
        Snapshot source = CreateSnapshot();
        Snapshot dest = CreateSnapshot();

        Plan plan = _sut.BuildPlan(BackupMode.Sync, source, dest);

        Assert.Multiple(() =>
        {
            Assert.That(plan.MissingFiles, Is.Empty);
            Assert.That(plan.CommonFiles, Is.Empty);
            Assert.That(plan.ExtraFiles, Is.Empty);
            Assert.That(plan.DirectoriesToCreate, Is.Empty);
            Assert.That(plan.TopLevelExtraDirectories, Is.Empty);
        });
    }

    [Test]
    public void BuildPlan_SyncMode_TopLevelExtraDirectories_FiltersNestedDirs()
    {
        Snapshot source = CreateSnapshot();
        Snapshot dest = CreateSnapshot(
            dirPaths: ["extra", "extra/nested", "extra/nested/deep", "other"]
        );

        Plan plan = _sut.BuildPlan(BackupMode.Sync, source, dest);

        var topLevel = plan.TopLevelExtraDirectories.Select(d => d.Value).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(topLevel, Does.Contain("extra"));
            Assert.That(topLevel, Does.Contain("other"));
            Assert.That(topLevel, Does.Not.Contain("extra/nested"));
            Assert.That(topLevel, Does.Not.Contain("extra/nested/deep"));
        });
    }

    [Test]
    public void BuildPlan_SyncMode_DirectoriesToCreate_SortedByPathLength()
    {
        Snapshot source = CreateSnapshot(
            dirPaths: ["a/b/c", "a", "a/b"]
        );
        Snapshot dest = CreateSnapshot();

        Plan plan = _sut.BuildPlan(BackupMode.Sync, source, dest);

        var lengths = plan.DirectoriesToCreate.Select(d => d.Value.Length).ToList();
        Assert.That(lengths, Is.Ordered);
    }
}

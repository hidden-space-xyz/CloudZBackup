namespace CloudZBackup.Tests.Unit.Application;

using CloudZBackup.Application.Services;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Comparers;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="PlanService"/>.
/// </summary>
[TestFixture]
public sealed class PlanServiceTests
{
    private readonly IEqualityComparer<RelativePath> comparer = new RelativePathComparer(
        OperatingSystem.IsWindows());

    private readonly PlanService sut = new();

    /// <summary>
    /// Verifies that building a plan in <see cref="BackupMode.Add"/> mode identifies only
    /// missing files and directories, without including extras.
    /// </summary>
    [Test]
    public void BuildPlanAddModeOnlyIdentifiesMissingFilesAndDirsNoExtras()
    {
        Snapshot source = this.CreateSnapshot(filePaths: ["shared.txt", "new.txt"], dirPaths: ["dirA"]);
        Snapshot dest = this.CreateSnapshot(filePaths: ["shared.txt", "old.txt"], dirPaths: ["dirB"]);

        Plan plan = this.sut.BuildPlan(BackupMode.Add, source, dest);

        Assert.Multiple(() =>
        {
            Assert.That(plan.MissingFiles.Select(f => f.Value), Does.Contain("new.txt"));
            Assert.That(plan.CommonFiles, Is.Empty);
            Assert.That(plan.ExtraFiles, Is.Empty);
            Assert.That(plan.TopLevelExtraDirectories, Is.Empty);
            Assert.That(plan.DirectoriesToCreate.Select(d => d.Value), Does.Contain("dirA"));
        });
    }

    /// <summary>
    /// Verifies that building a plan from two empty snapshots produces an empty plan.
    /// </summary>
    [Test]
    public void BuildPlanEmptySnapshotsReturnsEmptyPlan()
    {
        Snapshot source = this.CreateSnapshot();
        Snapshot dest = this.CreateSnapshot();

        Plan plan = this.sut.BuildPlan(BackupMode.Sync, source, dest);

        Assert.Multiple(() =>
        {
            Assert.That(plan.MissingFiles, Is.Empty);
            Assert.That(plan.CommonFiles, Is.Empty);
            Assert.That(plan.ExtraFiles, Is.Empty);
            Assert.That(plan.DirectoriesToCreate, Is.Empty);
            Assert.That(plan.TopLevelExtraDirectories, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that building a plan in <see cref="BackupMode.Remove"/> mode identifies
    /// only extra files and directories present in the destination.
    /// </summary>
    [Test]
    public void BuildPlanRemoveModeOnlyIdentifiesExtraFilesAndDirs()
    {
        Snapshot source = this.CreateSnapshot(filePaths: ["shared.txt"], dirPaths: ["dirA"]);
        Snapshot dest = this.CreateSnapshot(
            filePaths: ["shared.txt", "extra.txt"],
            dirPaths: ["dirA", "extraDir"]);

        Plan plan = this.sut.BuildPlan(BackupMode.Remove, source, dest);

        Assert.Multiple(() =>
        {
            Assert.That(plan.MissingFiles, Is.Empty);
            Assert.That(plan.CommonFiles, Is.Empty);
            Assert.That(plan.DirectoriesToCreate, Is.Empty);
            Assert.That(plan.ExtraFiles.Select(f => f.Value), Does.Contain("extra.txt"));
            Assert.That(
                plan.TopLevelExtraDirectories.Select(d => d.Value),
                Does.Contain("extraDir"));
        });
    }

    /// <summary>
    /// Verifies that directories to create are sorted by path length so parents are
    /// created before children.
    /// </summary>
    [Test]
    public void BuildPlanSyncModeDirectoriesToCreateSortedByPathLength()
    {
        Snapshot source = this.CreateSnapshot(dirPaths: ["a/b/c", "a", "a/b"]);
        Snapshot dest = this.CreateSnapshot();

        Plan plan = this.sut.BuildPlan(BackupMode.Sync, source, dest);

        var lengths = plan.DirectoriesToCreate.Select(d => d.Value.Length).ToList();
        Assert.That(lengths, Is.Ordered);
    }

    /// <summary>
    /// Verifies that building a plan in <see cref="BackupMode.Sync"/> mode correctly
    /// classifies files as missing, common, or extra.
    /// </summary>
    [Test]
    public void BuildPlanSyncModeIdentifiesMissingCommonAndExtraFiles()
    {
        Snapshot source = this.CreateSnapshot(filePaths: ["shared.txt", "new.txt"], dirPaths: ["dirA"]);
        Snapshot dest = this.CreateSnapshot(filePaths: ["shared.txt", "old.txt"], dirPaths: ["dirB"]);

        Plan plan = this.sut.BuildPlan(BackupMode.Sync, source, dest);

        Assert.Multiple(() =>
        {
            Assert.That(plan.MissingFiles.Select(f => f.Value), Does.Contain("new.txt"));
            Assert.That(plan.CommonFiles.Select(f => f.Value), Does.Contain("shared.txt"));
            Assert.That(plan.ExtraFiles.Select(f => f.Value), Does.Contain("old.txt"));
            Assert.That(plan.DirectoriesToCreate.Select(d => d.Value), Does.Contain("dirA"));
        });
    }

    /// <summary>
    /// Verifies that <see cref="Plan.TopLevelExtraDirectories"/> filters out nested
    /// subdirectories, keeping only top-level entries.
    /// </summary>
    [Test]
    public void BuildPlanSyncModeTopLevelExtraDirectoriesFiltersNestedDirs()
    {
        Snapshot source = this.CreateSnapshot();
        Snapshot dest = this.CreateSnapshot(
            dirPaths: ["extra", "extra/nested", "extra/nested/deep", "other"]);

        Plan plan = this.sut.BuildPlan(BackupMode.Sync, source, dest);

        var topLevel = plan.TopLevelExtraDirectories.Select(d => d.Value).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(topLevel, Does.Contain("extra"));
            Assert.That(topLevel, Does.Contain("other"));
            Assert.That(topLevel, Does.Not.Contain("extra/nested"));
            Assert.That(topLevel, Does.Not.Contain("extra/nested/deep"));
        });
    }

    private Snapshot CreateSnapshot(
        IEnumerable<string>? filePaths = null,
        IEnumerable<string>? dirPaths = null)
    {
        var files = new Dictionary<RelativePath, FileEntry>(this.comparer);
        var dirs = new HashSet<RelativePath>(this.comparer);

        foreach (string f in filePaths ?? [])
        {
            var rp = new RelativePath(f);
            files[rp] = new FileEntry(rp, 100, DateTime.UtcNow);
        }

        foreach (string d in dirPaths ?? [])
        {
            dirs.Add(new RelativePath(d));
        }

        return new Snapshot(files, dirs);
    }
}

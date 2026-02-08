using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Tests.Unit.Domain;

[TestFixture]
public sealed class SnapshotTests
{
    [Test]
    public void Snapshot_StoresFilesAndDirectories()
    {
        var path = new RelativePath("src/file.cs");
        var entry = new FileEntry(path, 512, DateTime.UtcNow);
        var files = new Dictionary<RelativePath, FileEntry> { [path] = entry };
        var dirs = new HashSet<RelativePath> { new("src") };

        var snapshot = new Snapshot(files, dirs);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Files, Has.Count.EqualTo(1));
            Assert.That(snapshot.Files.ContainsKey(path), Is.True);
            Assert.That(snapshot.Directories, Has.Count.EqualTo(1));
            Assert.That(snapshot.Directories.Contains(new RelativePath("src")), Is.True);
        });
    }

    [Test]
    public void Snapshot_EmptyCollections_IsValid()
    {
        var snapshot = new Snapshot(
            new Dictionary<RelativePath, FileEntry>(),
            new HashSet<RelativePath>()
        );

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Files, Is.Empty);
            Assert.That(snapshot.Directories, Is.Empty);
        });
    }
}

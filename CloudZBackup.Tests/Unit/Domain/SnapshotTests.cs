namespace CloudZBackup.Tests.Unit.Domain;

using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Snapshot"/>.
/// </summary>
[TestFixture]
public sealed class SnapshotTests
{
    /// <summary>
    /// Verifies that a <see cref="Snapshot"/> constructed with empty collections
    /// has empty <see cref="Snapshot.Files"/> and <see cref="Snapshot.Directories"/>.
    /// </summary>
    [Test]
    public void SnapshotEmptyCollectionsIsValid()
    {
        var snapshot = new Snapshot(
            new Dictionary<RelativePath, FileEntry>(),
            new HashSet<RelativePath>());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Files, Is.Empty);
            Assert.That(snapshot.Directories, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that a <see cref="Snapshot"/> correctly stores the provided files and directories.
    /// </summary>
    [Test]
    public void SnapshotStoresFilesAndDirectories()
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
}

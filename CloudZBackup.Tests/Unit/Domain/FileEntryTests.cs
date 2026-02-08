using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Tests.Unit.Domain;

[TestFixture]
public sealed class FileEntryTests
{
    [Test]
    public void Properties_ReturnConstructorValues()
    {
        var path = new RelativePath("docs/readme.md");
        var lastWrite = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var entry = new FileEntry(path, 1024L, lastWrite);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Path, Is.EqualTo(path));
            Assert.That(entry.Length, Is.EqualTo(1024L));
            Assert.That(entry.LastWriteTimeUtc, Is.EqualTo(lastWrite));
        });
    }

    [Test]
    public void Equality_SameValues_AreEqual()
    {
        var path = new RelativePath("file.txt");
        var time = DateTime.UtcNow;
        var a = new FileEntry(path, 100, time);
        var b = new FileEntry(path, 100, time);

        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Equality_DifferentLength_AreNotEqual()
    {
        var path = new RelativePath("file.txt");
        var time = DateTime.UtcNow;
        var a = new FileEntry(path, 100, time);
        var b = new FileEntry(path, 200, time);

        Assert.That(a, Is.Not.EqualTo(b));
    }
}

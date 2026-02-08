namespace CloudZBackup.Tests.Unit.Domain;

using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="FileEntry"/>.
/// </summary>
[TestFixture]
public sealed class FileEntryTests
{
    /// <summary>
    /// Verifies that two <see cref="FileEntry"/> instances with different lengths are not equal.
    /// </summary>
    [Test]
    public void EqualityDifferentLengthAreNotEqual()
    {
        var path = new RelativePath("file.txt");
        var time = DateTime.UtcNow;
        var a = new FileEntry(path, 100, time);
        var b = new FileEntry(path, 200, time);

        Assert.That(a, Is.Not.EqualTo(b));
    }

    /// <summary>
    /// Verifies that two <see cref="FileEntry"/> instances with identical values are equal.
    /// </summary>
    [Test]
    public void EqualitySameValuesAreEqual()
    {
        var path = new RelativePath("file.txt");
        var time = DateTime.UtcNow;
        var a = new FileEntry(path, 100, time);
        var b = new FileEntry(path, 100, time);

        Assert.That(a, Is.EqualTo(b));
    }

    /// <summary>
    /// Verifies that <see cref="FileEntry"/> properties return the values provided to the constructor.
    /// </summary>
    [Test]
    public void PropertiesReturnConstructorValues()
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
}

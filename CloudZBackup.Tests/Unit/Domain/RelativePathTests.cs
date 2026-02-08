using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Tests.Unit.Domain;

[TestFixture]
public sealed class RelativePathTests
{
    [Test]
    public void Constructor_NormalizesBackslashesToForwardSlashes()
    {
        var rp = new RelativePath(@"folder\subfolder\file.txt");

        Assert.That(rp.Value, Is.EqualTo("folder/subfolder/file.txt"));
    }

    [Test]
    public void Constructor_LeadingSlash_ThrowsOnWindows_OrTrimsOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Throws<ArgumentException>(() => _ = new RelativePath("/folder/file.txt"));
        }
        else
        {
            var rp = new RelativePath("/folder/file.txt");
            Assert.That(rp.Value, Is.EqualTo("folder/file.txt"));
        }
    }

    [Test]
    public void Constructor_EmptyOrWhitespace_ReturnsEmptyValue()
    {
        var empty = new RelativePath("");
        var whitespace = new RelativePath("   ");

        Assert.Multiple(() =>
        {
            Assert.That(empty.Value, Is.EqualTo(string.Empty));
            Assert.That(whitespace.Value, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void Constructor_ThrowsForTraversalSegments()
    {
        Assert.Throws<ArgumentException>(() => _ = new RelativePath("folder/../secret"));
    }

    [Test]
    public void Constructor_ThrowsForRootedPath()
    {
        string rooted = OperatingSystem.IsWindows() ? @"C:\absolute\path" : "/absolute/path";

        Assert.Throws<ArgumentException>(() => _ = new RelativePath(rooted));
    }

    [Test]
    public void FromSystem_CreatesEquivalentInstance()
    {
        var rp = RelativePath.FromSystem("a/b/c.txt");

        Assert.That(rp.Value, Is.EqualTo("a/b/c.txt"));
    }

    [Test]
    public void ToSystemPath_ReturnsNativeSeparators()
    {
        var rp = new RelativePath("folder/sub/file.txt");
        string system = rp.ToSystemPath();

        string expected = Path.Combine("folder", "sub", "file.txt");
        Assert.That(system, Is.EqualTo(expected));
    }

    [Test]
    public void ToString_ReturnsValue()
    {
        var rp = new RelativePath("docs/readme.md");

        Assert.That(rp.ToString(), Is.EqualTo("docs/readme.md"));
    }

    [Test]
    public void Equality_SameValue_AreEqual()
    {
        var a = new RelativePath("folder/file.txt");
        var b = new RelativePath("folder/file.txt");

        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = new RelativePath("folder/file1.txt");
        var b = new RelativePath("folder/file2.txt");

        Assert.That(a, Is.Not.EqualTo(b));
    }
}

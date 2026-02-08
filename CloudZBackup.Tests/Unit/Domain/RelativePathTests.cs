namespace CloudZBackup.Tests.Unit.Domain;

using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="RelativePath"/>.
/// </summary>
[TestFixture]
public sealed class RelativePathTests
{
    /// <summary>
    /// Verifies that constructing a <see cref="RelativePath"/> with an empty or whitespace string
    /// produces an instance whose value is <see cref="string.Empty"/>.
    /// </summary>
    [Test]
    public void ConstructorEmptyOrWhitespaceReturnsEmptyValue()
    {
        var empty = new RelativePath(string.Empty);
        var whitespace = new RelativePath("   ");

        Assert.Multiple(() =>
        {
            Assert.That(empty.Value, Is.EqualTo(string.Empty));
            Assert.That(whitespace.Value, Is.EqualTo(string.Empty));
        });
    }

    /// <summary>
    /// Verifies platform-specific behavior for a leading slash: throws on Windows,
    /// trims the slash on Unix.
    /// </summary>
    [Test]
    public void ConstructorLeadingSlashThrowsOnWindowsOrTrimsOnUnix()
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

    /// <summary>
    /// Verifies that backslashes are normalized to forward slashes during construction.
    /// </summary>
    [Test]
    public void ConstructorNormalizesBackslashesToForwardSlashes()
    {
        var rp = new RelativePath(@"folder\subfolder\file.txt");

        Assert.That(rp.Value, Is.EqualTo("folder/subfolder/file.txt"));
    }

    /// <summary>
    /// Verifies that constructing a <see cref="RelativePath"/> with a rooted path
    /// throws <see cref="ArgumentException"/>.
    /// </summary>
    [Test]
    public void ConstructorThrowsForRootedPath()
    {
        string rooted = OperatingSystem.IsWindows() ? @"C:\absolute\path" : "/absolute/path";

        Assert.Throws<ArgumentException>(() => _ = new RelativePath(rooted));
    }

    /// <summary>
    /// Verifies that constructing a <see cref="RelativePath"/> with directory traversal segments
    /// throws <see cref="ArgumentException"/>.
    /// </summary>
    [Test]
    public void ConstructorThrowsForTraversalSegments()
    {
        Assert.Throws<ArgumentException>(() => _ = new RelativePath("folder/../secret"));
    }

    /// <summary>
    /// Verifies that two <see cref="RelativePath"/> instances with different values are not equal.
    /// </summary>
    [Test]
    public void EqualityDifferentValueAreNotEqual()
    {
        var a = new RelativePath("folder/file1.txt");
        var b = new RelativePath("folder/file2.txt");

        Assert.That(a, Is.Not.EqualTo(b));
    }

    /// <summary>
    /// Verifies that two <see cref="RelativePath"/> instances with the same value are equal.
    /// </summary>
    [Test]
    public void EqualitySameValueAreEqual()
    {
        var a = new RelativePath("folder/file.txt");
        var b = new RelativePath("folder/file.txt");

        Assert.That(a, Is.EqualTo(b));
    }

    /// <summary>
    /// Verifies that <see cref="RelativePath.FromSystem"/> creates an equivalent instance.
    /// </summary>
    [Test]
    public void FromSystemCreatesEquivalentInstance()
    {
        var rp = RelativePath.FromSystem("a/b/c.txt");

        Assert.That(rp.Value, Is.EqualTo("a/b/c.txt"));
    }

    /// <summary>
    /// Verifies that <see cref="RelativePath.ToString"/> returns the stored value.
    /// </summary>
    [Test]
    public void ToStringReturnsValue()
    {
        var rp = new RelativePath("docs/readme.md");

        Assert.That(rp.ToString(), Is.EqualTo("docs/readme.md"));
    }

    /// <summary>
    /// Verifies that <see cref="RelativePath.ToSystemPath"/> returns the path
    /// with platform-native directory separators.
    /// </summary>
    [Test]
    public void ToSystemPathReturnsNativeSeparators()
    {
        var rp = new RelativePath("folder/sub/file.txt");
        string system = rp.ToSystemPath();

        string expected = Path.Combine("folder", "sub", "file.txt");
        Assert.That(system, Is.EqualTo(expected));
    }
}

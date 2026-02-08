namespace CloudZBackup.Tests.Unit.Domain;

using CloudZBackup.Domain.Comparers;
using CloudZBackup.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="RelativePathComparer"/>.
/// </summary>
[TestFixture]
public sealed class RelativePathComparerTests
{
    /// <summary>
    /// Verifies that a case-insensitive comparer treats paths differing only in case as equal.
    /// </summary>
    [Test]
    public void CaseInsensitiveSamePathDifferentCaseAreEqual()
    {
        var comparer = new RelativePathComparer(ignoreCase: true);
        var a = new RelativePath("Folder/File.TXT");
        var b = new RelativePath("folder/file.txt");

        Assert.Multiple(() =>
        {
            Assert.That(comparer.Equals(a, b), Is.True);
            Assert.That(comparer.GetHashCode(a), Is.EqualTo(comparer.GetHashCode(b)));
        });
    }

    /// <summary>
    /// Verifies that a case-sensitive comparer treats identical paths as equal.
    /// </summary>
    [Test]
    public void CaseSensitiveSamePathAreEqual()
    {
        var comparer = new RelativePathComparer(ignoreCase: false);
        var a = new RelativePath("folder/file.txt");
        var b = new RelativePath("folder/file.txt");

        Assert.Multiple(() =>
        {
            Assert.That(comparer.Equals(a, b), Is.True);
            Assert.That(comparer.GetHashCode(a), Is.EqualTo(comparer.GetHashCode(b)));
        });
    }

    /// <summary>
    /// Verifies that a case-sensitive comparer treats paths differing in case as not equal.
    /// </summary>
    [Test]
    public void CaseSensitiveSamePathDifferentCaseAreNotEqual()
    {
        var comparer = new RelativePathComparer(ignoreCase: false);
        var a = new RelativePath("Folder/File.TXT");
        var b = new RelativePath("folder/file.txt");

        Assert.That(comparer.Equals(a, b), Is.False);
    }

    /// <summary>
    /// Verifies that different paths are not considered equal by the comparer.
    /// </summary>
    [Test]
    public void DifferentPathsAreNotEqual()
    {
        var comparer = new RelativePathComparer(ignoreCase: true);
        var a = new RelativePath("folder/a.txt");
        var b = new RelativePath("folder/b.txt");

        Assert.That(comparer.Equals(a, b), Is.False);
    }
}

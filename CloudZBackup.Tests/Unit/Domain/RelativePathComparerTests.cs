using CloudZBackup.Domain.Comparers;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Tests.Unit.Domain;

[TestFixture]
public sealed class RelativePathComparerTests
{
    [Test]
    public void CaseInsensitive_SamePathDifferentCase_AreEqual()
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

    [Test]
    public void CaseSensitive_SamePathDifferentCase_AreNotEqual()
    {
        var comparer = new RelativePathComparer(ignoreCase: false);
        var a = new RelativePath("Folder/File.TXT");
        var b = new RelativePath("folder/file.txt");

        Assert.That(comparer.Equals(a, b), Is.False);
    }

    [Test]
    public void CaseSensitive_SamePath_AreEqual()
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

    [Test]
    public void DifferentPaths_AreNotEqual()
    {
        var comparer = new RelativePathComparer(ignoreCase: true);
        var a = new RelativePath("folder/a.txt");
        var b = new RelativePath("folder/b.txt");

        Assert.That(comparer.Equals(a, b), Is.False);
    }
}

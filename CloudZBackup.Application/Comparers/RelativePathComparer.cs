using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Comparers;

public sealed class RelativePathComparer(bool ignoreCase) : IEqualityComparer<RelativePath>
{
    private readonly StringComparer comparer = ignoreCase
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public bool Equals(RelativePath x, RelativePath y)
    {
        return comparer.Equals(x.Value, y.Value);
    }

    public int GetHashCode(RelativePath obj)
    {
        return comparer.GetHashCode(obj.Value);
    }
}

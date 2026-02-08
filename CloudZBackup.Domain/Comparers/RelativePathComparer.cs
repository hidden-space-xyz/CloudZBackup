using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Domain.Comparers;

/// <summary>
/// Provides equality comparison for <see cref="RelativePath"/> instances,
/// optionally ignoring case to support case-insensitive file systems.
/// </summary>
/// <param name="ignoreCase">
/// <see langword="true"/> to perform case-insensitive comparison (e.g., Windows);
/// <see langword="false"/> for case-sensitive comparison (e.g., Linux).
/// </param>
public sealed class RelativePathComparer(bool ignoreCase) : IEqualityComparer<RelativePath>
{
    private readonly StringComparer _comparer = ignoreCase
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <inheritdoc />
    public bool Equals(RelativePath x, RelativePath y)
    {
        return _comparer.Equals(x.Value, y.Value);
    }

    /// <inheritdoc />
    public int GetHashCode(RelativePath obj)
    {
        return _comparer.GetHashCode(obj.Value);
    }
}

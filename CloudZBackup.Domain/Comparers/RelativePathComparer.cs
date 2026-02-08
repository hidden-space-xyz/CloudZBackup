namespace CloudZBackup.Domain.Comparers;

using CloudZBackup.Domain.ValueObjects;

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
    private readonly StringComparer comparer = ignoreCase
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <inheritdoc />
    public bool Equals(RelativePath x, RelativePath y)
    {
        return this.comparer.Equals(x.Value, y.Value);
    }

    /// <inheritdoc />
    public int GetHashCode(RelativePath obj)
    {
        return this.comparer.GetHashCode(obj.Value);
    }
}

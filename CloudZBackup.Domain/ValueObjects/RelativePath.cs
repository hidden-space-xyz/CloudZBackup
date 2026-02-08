using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CloudZBackup.Domain.ValueObjects;

/// <summary>
/// A value object representing a normalized, forward-slash-separated relative file-system path.
/// Guarantees that the path is not rooted and does not contain traversal (<c>..</c>) segments.
/// </summary>
[DebuggerDisplay("{Value}")]
public readonly record struct RelativePath
{
    /// <summary>
    /// Gets the normalized path value using forward slashes as separators.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new <see cref="RelativePath"/> instance after normalizing and validating the supplied value.
    /// </summary>
    /// <param name="value">The raw path string to normalize.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is an absolute (rooted) path or contains <c>..</c> traversal segments.
    /// </exception>
    public RelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Value = string.Empty;
            return;
        }

        string normalized = value.Replace('\\', '/').TrimStart('/');

        if (Path.IsPathRooted(value))
            throw new ArgumentException("RelativePath cannot be rooted.", nameof(value));

        if (ContainsTraversalSegment(normalized))
            throw new ArgumentException(
                "RelativePath cannot contain '..' segments.",
                nameof(value)
            );

        Value = normalized;
    }

    /// <summary>
    /// Creates a <see cref="RelativePath"/> from a platform-specific relative path string.
    /// </summary>
    /// <param name="relative">A relative path using the current platform's directory separator.</param>
    /// <returns>A new <see cref="RelativePath"/> instance.</returns>
    public static RelativePath FromSystem(string relative)
    {
        return new(relative);
    }

    /// <summary>
    /// Converts this relative path to the current platform's directory-separator convention.
    /// On Unix-like systems (where the separator is already <c>/</c>) the value is returned as-is.
    /// </summary>
    /// <returns>The path string with platform-native separators.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToSystemPath()
    {
        if (string.IsNullOrEmpty(Value) || Path.DirectorySeparatorChar == '/')
            return Value ?? string.Empty;

        return Value.Replace('/', Path.DirectorySeparatorChar);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }

    /// <summary>
    /// Checks whether the normalized path contains any <c>..</c> traversal segments
    /// by scanning character spans without allocating intermediate arrays.
    /// </summary>
    private static bool ContainsTraversalSegment(ReadOnlySpan<char> path)
    {
        while (!path.IsEmpty)
        {
            int sep = path.IndexOf('/');
            ReadOnlySpan<char> segment = sep < 0 ? path : path[..sep];

            if (segment is "..")
                return true;

            if (sep < 0)
                break;

            path = path[(sep + 1)..];
        }

        return false;
    }
}

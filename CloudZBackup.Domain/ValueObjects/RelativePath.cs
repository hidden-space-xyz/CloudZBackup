using System.Diagnostics;

namespace CloudZBackup.Domain.ValueObjects;

[DebuggerDisplay("{Value}")]
public readonly record struct RelativePath
{
    public string Value { get; }

    public RelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Value = string.Empty;
            return;
        }

        var normalized = value.Replace('\\', '/').TrimStart('/');

        if (Path.IsPathRooted(value))
            throw new ArgumentException("RelativePath cannot be rooted.", nameof(value));

        // Prevent traversal segments
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(s => s == ".."))
            throw new ArgumentException(
                "RelativePath cannot contain '..' segments.",
                nameof(value)
            );

        Value = normalized;
    }

    public static RelativePath FromSystem(string relative) => new(relative);

    public string ToSystemPath() =>
        string.IsNullOrEmpty(Value)
            ? string.Empty
            : Value.Replace('/', Path.DirectorySeparatorChar);

    public override string ToString() => Value;
}

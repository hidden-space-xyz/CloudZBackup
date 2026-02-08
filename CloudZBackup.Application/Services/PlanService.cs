using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Comparers;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services;

/// <summary>
/// Compares source and destination <see cref="Snapshot"/> instances to produce an
/// actionable <see cref="Plan"/> describing which directories and files need to be
/// created, copied, overwritten, or deleted.
/// </summary>
public sealed class PlanService : IPlanService
{
    private readonly IEqualityComparer<RelativePath> _relativePathComparer =
        new RelativePathComparer(OperatingSystem.IsWindows());

    /// <inheritdoc />
    public Plan BuildPlan(BackupMode mode, Snapshot source, Snapshot dest)
    {
        var directoriesToCreate = new List<RelativePath>();
        var missingFiles = new List<RelativePath>();
        var commonFiles = new List<RelativePath>();
        var extraFiles = new List<RelativePath>();
        var topLevelExtraDirectories = new List<RelativePath>();

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            foreach (RelativePath dir in source.Directories)
            {
                if (!dest.Directories.Contains(dir))
                    directoriesToCreate.Add(dir);
            }

            directoriesToCreate.Sort((a, b) => a.Value.Length.CompareTo(b.Value.Length));

            foreach (RelativePath file in source.Files.Keys)
            {
                if (!dest.Files.ContainsKey(file))
                    missingFiles.Add(file);
                else if (mode == BackupMode.Sync)
                    commonFiles.Add(file);
            }
        }

        if (mode is BackupMode.Sync or BackupMode.Remove)
        {
            foreach (RelativePath file in dest.Files.Keys)
            {
                if (!source.Files.ContainsKey(file))
                    extraFiles.Add(file);
            }

            var extraDirs = new HashSet<RelativePath>(_relativePathComparer);
            foreach (RelativePath dir in dest.Directories)
            {
                if (!source.Directories.Contains(dir))
                    extraDirs.Add(dir);
            }

            topLevelExtraDirectories = ComputeTopLevelDirectories(extraDirs);
        }

        return new Plan(
            directoriesToCreate,
            missingFiles,
            commonFiles,
            extraFiles,
            topLevelExtraDirectories
        );
    }

    /// <summary>
    /// Filters a set of directories down to only those that have no ancestor in the same set,
    /// so that a recursive delete on these top-level entries covers all nested directories.
    /// Uses a pre-built lookup of normalized values to avoid re-validating paths inside the loop.
    /// </summary>
    private List<RelativePath> ComputeTopLevelDirectories(HashSet<RelativePath> extras)
    {
        StringComparer stringComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        HashSet<string> rawValues = new(extras.Count, stringComparer);
        foreach (RelativePath rp in extras)
            rawValues.Add(rp.Value);

        List<RelativePath> result = new(extras.Count);
        foreach (RelativePath d in extras)
        {
            if (!HasAncestorInSet(d.Value, rawValues))
                result.Add(d);
        }
        return result;

        static bool HasAncestorInSet(string value, HashSet<string> set)
        {
            ReadOnlySpan<char> span = value.AsSpan();

            while (true)
            {
                int idx = span.LastIndexOf('/');
                if (idx <= 0)
                    return false;

                span = span[..idx];
                if (set.Contains(span.ToString()))
                    return true;
            }
        }
    }
}

using CloudZBackup.Application.Comparers;
using CloudZBackup.Application.Services.Interfaces;
using CloudZBackup.Application.ValueObjects;
using CloudZBackup.Domain.Enums;
using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.Services;

public sealed class PlanService : IPlanService
{
    private readonly IEqualityComparer<RelativePath> relativePathComparer =
        new RelativePathComparer(OperatingSystem.IsWindows());

    public Plan BuildPlan(BackupMode mode, Snapshot source, Snapshot dest)
    {
        var dirsToCreate = new List<RelativePath>();
        var missingFiles = new List<RelativePath>();
        var commonFiles = new List<RelativePath>();
        var extraFiles = new List<RelativePath>();
        var topLevelExtraDirs = new List<RelativePath>();

        if (mode is BackupMode.Sync or BackupMode.Add)
        {
            foreach (RelativePath dir in source.Directories)
            {
                if (!dest.Directories.Contains(dir))
                    dirsToCreate.Add(dir);
            }

            // Ensure parent dirs are created before children
            dirsToCreate.Sort((a, b) => a.Value.Length.CompareTo(b.Value.Length));

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

            var extraDirs = new HashSet<RelativePath>(relativePathComparer);
            foreach (RelativePath dir in dest.Directories)
            {
                if (!source.Directories.Contains(dir))
                    extraDirs.Add(dir);
            }

            topLevelExtraDirs = ComputeTopLevelDirectories(extraDirs);
        }

        return new Plan(dirsToCreate, missingFiles, commonFiles, extraFiles, topLevelExtraDirs);
    }

    private static List<RelativePath> ComputeTopLevelDirectories(HashSet<RelativePath> extras)
    {
        return extras.Where(d => !HasAncestorInSet(d, extras)).ToList();

        static bool HasAncestorInSet(RelativePath dir, HashSet<RelativePath> set)
        {
            string value = dir.Value;

            while (true)
            {
                int idx = value.LastIndexOf('/');
                if (idx <= 0)
                    return false;

                value = value[..idx];
                if (set.Contains(new RelativePath(value)))
                    return true;
            }
        }
    }
}

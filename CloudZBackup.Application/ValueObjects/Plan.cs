using CloudZBackup.Domain.ValueObjects;

namespace CloudZBackup.Application.ValueObjects;

public sealed record Plan(
    List<RelativePath> DirsToCreate,
    List<RelativePath> MissingFiles,
    List<RelativePath> CommonFiles,
    List<RelativePath> ExtraFiles,
    List<RelativePath> TopLevelExtraDirs
);
